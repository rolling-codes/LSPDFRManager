using System.Xml.Linq;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Creates, parses, previews, and installs OIV packages (.oiv files).
/// An OIV is a ZIP containing assembly.xml and a content/ folder.
/// </summary>
public static class OivService
{
    // ── Validator ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that a package has the required metadata and source files.
    /// </summary>
    public static (bool isValid, string? error) ValidatePackage(OivPackage pkg)
    {
        if (string.IsNullOrWhiteSpace(pkg.Name))
            return (false, "Package name is required.");

        if (pkg.Files is null || pkg.Files.Count == 0)
            return (false, "Package must contain at least one file.");

        foreach (var entry in pkg.Files)
        {
            if (string.IsNullOrWhiteSpace(entry.SourcePath))
                return (false, $"File entry has no source path.");

            if (string.IsNullOrWhiteSpace(entry.InstallPath))
                return (false, $"File entry '{entry.SourcePath}' has no install path.");

            if (!File.Exists(entry.SourcePath))
                return (false, $"Source file not found: {entry.SourcePath}");
        }

        return (true, null);
    }

    // ── Creator ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an .oiv package ZIP at <paramref name="outputPath"/> from the given package definition.
    /// </summary>
    public static bool CreatePackage(OivPackage package, string outputPath)
    {
        AppLogger.Info($"[OIV_CREATE] Creating package '{package.Name}' -> {outputPath}");

        var (isValid, error) = ValidatePackage(package);
        if (!isValid)
        {
            AppLogger.Error($"[OIV_CREATE] Validation failed: {error}");
            package.IsValid = false;
            package.ValidationError = error;
            return false;
        }

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            // Write assembly.xml
            var assemblyXml = BuildAssemblyXml(package);
            var xmlEntry = zip.CreateEntry("assembly.xml");
            using (var xmlStream = xmlEntry.Open())
            using (var writer = new StreamWriter(xmlStream, System.Text.Encoding.UTF8))
            {
                writer.Write(assemblyXml);
            }

            // Write content/ files
            foreach (var file in package.Files)
            {
                var entryName = "content/" + NormalizeEntryName(file.InstallPath);
                var contentEntry = zip.CreateEntry(entryName);
                using var entryStream = contentEntry.Open();
                using var fileStream = File.OpenRead(file.SourcePath);
                fileStream.CopyTo(entryStream);
            }

            AppLogger.Info($"[OIV_CREATE] Package created with {package.Files.Count} file(s): {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[OIV_CREATE] Failed to create package: {outputPath}", ex);
            return false;
        }
    }

    private static string BuildAssemblyXml(OivPackage package)
    {
        var versionParts = package.Version.Split('.');
        var major = versionParts.Length > 0 ? versionParts[0] : "1";
        var minor = versionParts.Length > 1 ? versionParts[1] : "0";

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("package",
                new XAttribute("version", "2.0"),
                new XElement("metadata",
                    new XElement("name", package.Name),
                    new XElement("version",
                        new XElement("major", major),
                        new XElement("minor", minor)),
                    new XElement("author", package.Author),
                    new XElement("description",
                        new XCData(package.Description))),
                new XElement("content",
                    package.Files.Select(f =>
                        new XElement("add",
                            new XAttribute("source", "content/" + NormalizeEntryName(f.InstallPath)),
                            f.InstallPath)))));

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    private static string NormalizeEntryName(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    // ── Parser ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens an .oiv file and parses its assembly.xml into an OivPackage.
    /// Returns a package with IsValid=false and ValidationError set on failure.
    /// </summary>
    public static OivPackage ParsePackage(string oivPath)
    {
        AppLogger.Info($"[OIV_PARSE] Parsing {oivPath}");

        try
        {
            if (!File.Exists(oivPath))
                return InvalidPackage($"File not found: {oivPath}", oivPath);

            using var zip = ZipFile.OpenRead(oivPath);

            var xmlEntry = zip.GetEntry("assembly.xml");
            if (xmlEntry is null)
                return InvalidPackage("assembly.xml not found in OIV package.", oivPath);

            XDocument doc;
            try
            {
                using var stream = xmlEntry.Open();
                doc = XDocument.Load(stream);
            }
            catch (Exception ex)
            {
                return InvalidPackage($"assembly.xml parse error: {ex.Message}", oivPath);
            }

            AppLogger.Info($"[OIV_VALIDATE] Validating assembly.xml structure");

            var root = doc.Root;
            if (root is null || root.Name != "package")
                return InvalidPackage("assembly.xml root element must be <package>.", oivPath);

            var meta = root.Element("metadata");
            if (meta is null)
                return InvalidPackage("assembly.xml missing <metadata> element.", oivPath);

            var name = meta.Element("name")?.Value ?? "";
            var author = meta.Element("author")?.Value ?? "";
            var description = meta.Element("description")?.Value ?? "";

            var verElem = meta.Element("version");
            var major = verElem?.Element("major")?.Value ?? "1";
            var minor = verElem?.Element("minor")?.Value ?? "0";
            var version = $"{major}.{minor}";

            var files = new List<OivFileEntry>();
            var contentElem = root.Element("content");
            if (contentElem is not null)
            {
                foreach (var add in contentElem.Descendants("add"))
                {
                    var source = add.Attribute("source")?.Value ?? "";
                    var installPath = add.Value ?? "";

                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(installPath))
                        continue;

                    files.Add(new OivFileEntry
                    {
                        SourcePath = source,
                        InstallPath = installPath,
                        Action = OivFileAction.Add
                    });
                }
            }

            AppLogger.Info($"[OIV_PARSE] Parsed '{name}' v{version} with {files.Count} file(s)");

            return new OivPackage
            {
                Name = name,
                Version = version,
                Author = author,
                Description = description,
                Files = files,
                SourcePath = oivPath,
                IsValid = true
            };
        }
        catch (InvalidDataException ex)
        {
            AppLogger.Error($"[OIV_ERROR] Not a valid ZIP/OIV: {oivPath}", ex);
            return InvalidPackage($"Not a valid OIV package: {ex.Message}", oivPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[OIV_ERROR] Parse failed: {oivPath}", ex);
            return InvalidPackage($"Parse error: {ex.Message}", oivPath);
        }
    }

    private static OivPackage InvalidPackage(string error, string? sourcePath = null)
    {
        AppLogger.Error($"[OIV_ERROR] {error}");
        return new OivPackage
        {
            SourcePath = sourcePath,
            IsValid = false,
            ValidationError = error
        };
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines whether each file in the package would be an Add, Replace, or Skip.
    /// Does not write any files.
    /// </summary>
    public static List<OivFileEntry> PreviewInstall(OivPackage pkg, string targetRoot)
    {
        var result = new List<OivFileEntry>();

        foreach (var entry in pkg.Files)
        {
            var targetPath = ResolveTargetPath(entry.InstallPath, targetRoot);
            var action = File.Exists(targetPath) ? OivFileAction.Replace : OivFileAction.Add;

            result.Add(new OivFileEntry
            {
                SourcePath = entry.SourcePath,
                InstallPath = entry.InstallPath,
                Action = action
            });
        }

        return result;
    }

    private static string ResolveTargetPath(string installPath, string targetRoot)
    {
        // If installPath is already absolute, use it; otherwise combine with targetRoot
        if (Path.IsPathRooted(installPath))
            return installPath;

        return Path.GetFullPath(Path.Combine(targetRoot, installPath));
    }

    // ── Installer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Installs all files from the OIV package to the target root directory.
    /// Backs up existing files before overwrite; rolls back on any failure.
    /// </summary>
    public static async Task<InstallResult> InstallPackage(OivPackage pkg, string targetRoot, CancellationToken ct = default)
    {
        AppLogger.Info($"[OIV_INSTALL] Installing '{pkg.Name}' to {targetRoot}");

        if (!pkg.IsValid)
        {
            AppLogger.Error($"[OIV_ERROR] Cannot install invalid package: {pkg.ValidationError}");
            return new InstallResult { Success = false, Error = pkg.ValidationError };
        }

        if (string.IsNullOrEmpty(pkg.SourcePath) || !File.Exists(pkg.SourcePath))
        {
            var err = $"OIV source file not found: {pkg.SourcePath}";
            AppLogger.Error($"[OIV_ERROR] {err}");
            return new InstallResult { Success = false, Error = err };
        }

        var writtenFiles = new List<string>();
        var backupRoot = Path.Combine(Path.GetTempPath(), $".oiv_rollback_{Guid.NewGuid():N}");
        var rollbackItems = new List<(string dest, string? backup)>();

        try
        {
            Directory.CreateDirectory(backupRoot);

            using var zip = ZipFile.OpenRead(pkg.SourcePath);

            foreach (var fileEntry in pkg.Files)
            {
                ct.ThrowIfCancellationRequested();

                var targetPath = ResolveTargetPath(fileEntry.InstallPath, targetRoot);
                var contentKey = fileEntry.SourcePath.Replace('\\', '/').TrimStart('/');

                var zipEntry = zip.GetEntry(contentKey);
                if (zipEntry is null)
                {
                    AppLogger.Info($"[OIV_SKIP_DUPLICATE] Entry not found in zip, skipping: {contentKey}");
                    continue;
                }

                // Ensure target directory exists
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                // Backup existing file
                string? backupPath = null;
                if (File.Exists(targetPath))
                {
                    backupPath = Path.Combine(backupRoot, Guid.NewGuid().ToString("N") + ".bak");
                    File.Copy(targetPath, backupPath, overwrite: false);
                    AppLogger.Info($"[OIV_BACKUP] {targetPath} -> {backupPath}");
                }

                // Write to temp then commit
                var tempPath = Path.Combine(targetDir ?? Path.GetTempPath(), $".oiv_{Guid.NewGuid():N}.tmp");
                try
                {
                    using (var entryStream = zipEntry.Open())
                    using (var destFile = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                    {
                        await entryStream.CopyToAsync(destFile, ct);
                    }

                    File.Move(tempPath, targetPath, overwrite: true);
                }
                catch
                {
                    if (File.Exists(tempPath))
                        try { File.Delete(tempPath); } catch { /* best-effort */ }
                    throw;
                }

                rollbackItems.Add((targetPath, backupPath));
                writtenFiles.Add(targetPath);
                AppLogger.Info($"[OIV_INSTALL] Written: {targetPath}");
            }

            // Clean up backups on success
            try { Directory.Delete(backupRoot, recursive: true); } catch { /* best-effort */ }

            AppLogger.Info($"[OIV_INSTALL] Complete. {writtenFiles.Count} file(s) installed.");
            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count,
                WrittenFiles = writtenFiles
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[OIV_ERROR] Install failed, rolling back {rollbackItems.Count} file(s)", ex);

            await RollbackAsync(rollbackItems);

            try { Directory.Delete(backupRoot, recursive: true); } catch { /* best-effort */ }

            return new InstallResult
            {
                Success = false,
                IsPartial = rollbackItems.Count > 0,
                FilesWritten = writtenFiles.Count,
                Error = ex.Message,
                FailedEntry = writtenFiles.Count < pkg.Files.Count
                    ? pkg.Files.ElementAtOrDefault(writtenFiles.Count)?.InstallPath
                    : null,
                WrittenFiles = []
            };
        }
    }

    private static Task RollbackAsync(List<(string dest, string? backup)> items)
    {
        foreach (var (dest, backup) in Enumerable.Reverse(items))
        {
            try
            {
                if (File.Exists(dest))
                    File.Delete(dest);

                if (backup is not null && File.Exists(backup))
                    File.Move(backup, dest);

                AppLogger.Info($"[OIV_ROLLBACK] Restored: {dest}");
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"[OIV_ROLLBACK] Failed to restore '{dest}': {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }
}
