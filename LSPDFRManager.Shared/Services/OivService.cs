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

            using var stream = xmlEntry.Open();
            return ParseFromStream(stream, oivPath);
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

    // Maximum assembly.xml size accepted before parsing (1 MB / ~1M chars).
    // Aligns with MaxCharactersInDocument so both limits tell the same story.
    private const long MaxAssemblyXmlBytes       = 1 * 1024 * 1024;
    private const long MaxAssemblyXmlChars        = 1_000_000;
    private const long MaxAssemblyXmlEntityChars  = 1_024;

    /// <summary>
    /// Parses an already-opened assembly.xml stream into an OivPackage.
    /// Used by SmartInstallPlanner, which has the stream buffered from the archive.
    /// Returns a package with IsValid=false and ValidationError set on failure.
    /// Caller owns and must dispose the stream; this method does not close it.
    /// </summary>
    public static OivPackage ParseFromStream(Stream assemblyXmlStream, string? sourcePath = null)
    {
        try
        {
            // ── Size gate ────────────────────────────────────────────────────────
            // For seekable streams check length directly. For non-seekable streams
            // (e.g. ZipArchiveEntry.Open()) read into a capped buffer so the limit
            // is enforced regardless of stream type.
            Stream parseStream;
            bool   ownedBuffer = false;

            if (assemblyXmlStream.CanSeek)
            {
                if (assemblyXmlStream.Length > MaxAssemblyXmlBytes)
                    return InvalidPackage(
                        $"assembly.xml exceeds the {MaxAssemblyXmlBytes / 1024} KB size limit.", sourcePath);
                parseStream = assemblyXmlStream;
            }
            else
            {
                // Read up to limit + 1 byte so we can detect oversize without reading the whole file.
                var buffer = new byte[MaxAssemblyXmlBytes + 1];
                int totalRead = 0, read;
                while (totalRead < buffer.Length &&
                       (read = assemblyXmlStream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
                    totalRead += read;

                if (totalRead > MaxAssemblyXmlBytes)
                    return InvalidPackage(
                        $"assembly.xml exceeds the {MaxAssemblyXmlBytes / 1024} KB size limit.", sourcePath);

                parseStream  = new MemoryStream(buffer, 0, totalRead, writable: false);
                ownedBuffer  = true;
            }

            XDocument doc;
            try
            {
                // XmlReader with hard limits prevents DTD-based DoS and unbounded
                // memory/CPU from attacker-controlled XML. CloseInput=false ensures
                // the caller-owned underlying stream is not closed when the reader
                // is disposed.
                var settings = new System.Xml.XmlReaderSettings
                {
                    DtdProcessing           = System.Xml.DtdProcessing.Prohibit,
                    MaxCharactersInDocument  = MaxAssemblyXmlChars,
                    MaxCharactersFromEntities = MaxAssemblyXmlEntityChars,
                    XmlResolver             = null,
                    CloseInput              = false,
                    IgnoreComments          = true,
                    IgnoreProcessingInstructions = true,
                };
                using var reader = System.Xml.XmlReader.Create(parseStream, settings);
                doc = XDocument.Load(reader, LoadOptions.None);
            }
            catch (Exception ex)
            {
                if (ownedBuffer) parseStream.Dispose();
                var hint = ex.Message.Contains("characters") || ex.Message.Contains("limit")
                    ? $" (limit: {MaxAssemblyXmlChars:N0} characters)"
                    : "";
                return InvalidPackage($"assembly.xml parse error: {ex.Message}{hint}", sourcePath);
            }
            finally
            {
                if (ownedBuffer) parseStream.Dispose();
            }

            AppLogger.Info($"[OIV_VALIDATE] Validating assembly.xml structure");

            var root = doc.Root;
            if (root is null || root.Name != "package")
                return InvalidPackage("assembly.xml root element must be <package>.", sourcePath);

            var meta = root.Element("metadata");
            if (meta is null)
                return InvalidPackage("assembly.xml missing <metadata> element.", sourcePath);

            var name = meta.Element("name")?.Value ?? "";
            var author = meta.Element("author")?.Value ?? "";
            var description = meta.Element("description")?.Value ?? "";
            // Support targetGame both inside <metadata> and at <package> root level.
            var targetGame = meta.Element("targetGame")?.Value
                ?? meta.Element("target")?.Value
                ?? root.Element("targetGame")?.Value
                ?? root.Element("target")?.Value
                ?? "";

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
                TargetGame = string.IsNullOrWhiteSpace(targetGame) ? "Grand Theft Auto V" : targetGame,
                Files = files,
                SourcePath = sourcePath,
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[OIV_ERROR] ParseFromStream failed: {sourcePath ?? "unknown"}", ex);
            return InvalidPackage($"Parse error: {ex.Message}", sourcePath);
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
