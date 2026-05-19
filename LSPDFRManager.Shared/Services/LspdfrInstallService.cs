using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Validates and executes a safe, layout-aware LSPDFR/RAGE install.
/// Inspects the archive before extraction (strips nested roots, skips junk),
/// validates the post-install GTA V layout, and reads the RAGE log for errors.
/// </summary>
public static class LspdfrInstallService
{
    // LSPDFR core file candidates — single source of truth is LspdfrInstallLocator
    // (Submission 1) so the install/validation layers can't drift apart. Index 0 is the
    // canonical file the official LSPDFR archive ships ("Plugins/LSPD First Response.dll");
    // the remaining entries (e.g. plugins/LSPDFR.dll) are tolerated legacy aliases.
    private static readonly string[] LspdfrCorePaths =
        LspdfrInstallLocator.LspdfrCoreCandidates.Select(c => c.Replace('\\', '/')).ToArray();

    private static string CanonicalLspdfrCore => LspdfrCorePaths[0];

    private static readonly HashSet<string> LspdfrCorePathsLower =
        new(LspdfrCorePaths.Select(p => p.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

    // Required non-core paths that must exist in GTA V root after a successful LSPDFR install.
    // The LSPDFR core is checked separately (any tolerated candidate counts).
    private static readonly string[] RequiredPaths =
    [
        "RAGEPluginHook.exe",
    ];

    // Directories that must exist (or have content) after install
    private static readonly string[] RequiredDirectories =
    [
        "plugins/lspdfr",
        "lspdfr",
    ];

    // Known LSPDFR archive entry names (normalised, relative to archive root) that signal
    // we've detected the correct top-level folder to strip. The LSPDFR core candidates
    // (canonical + legacy aliases) are also treated as signatures via IsSignatureEntry.
    private static readonly HashSet<string> LspdfrSignatureFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ragepluginhook.exe",
        "ragepluginhook.dll",
    };

    private static bool IsSignatureEntry(string lowerKey) =>
        LspdfrSignatureFiles.Contains(lowerKey) || LspdfrCorePathsLower.Contains(lowerKey);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the archive at <paramref name="sourcePath"/>, checks if it is an LSPDFR/RAGE package,
    /// and returns the classified manifest. Returns null if the archive is not LSPDFR-related or
    /// cannot be opened.
    /// </summary>
    public static LspdfrArchiveManifest? TryInspectArchive(string sourcePath)
    {
        try
        {
            if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(sourcePath);
                var adapter = new ZipArchiveAdapter(zip);
                return IsLspdfrArchive(adapter) ? InspectArchive(adapter) : null;
            }

            if (!Directory.Exists(sourcePath))
            {
                using var arc = SharpCompress.Archives.ArchiveFactory.Open(sourcePath);
                var adapter = new SharpCompressArchiveAdapter(arc);
                return IsLspdfrArchive(adapter) ? InspectArchive(adapter) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"[LSPDFR] Archive inspection failed for '{sourcePath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns true when the archive looks like an LSPDFR/RAGE package.
    /// </summary>
    public static bool IsLspdfrArchive(IArchive archive)
    {
        var keys = archive.Entries
            .Where(e => !e.IsDirectory)
            .Select(e => InstallerSafetyPolicy.NormalizeRelativePath(e.Key).ToLowerInvariant())
            .ToList();

        var stripped = StripArchiveRoot(keys);
        return stripped.Any(IsSignatureEntry);
    }

    /// <summary>
    /// Inspects the archive and returns a classified manifest — before any files are written.
    /// </summary>
    public static LspdfrArchiveManifest InspectArchive(IArchive archive)
    {
        var rawKeys = archive.Entries
            .Where(e => !e.IsDirectory)
            .Select(e => InstallerSafetyPolicy.NormalizeRelativePath(e.Key))
            .ToList();

        var root = DetectArchiveRoot(rawKeys);
        var entries = new List<LspdfrArchiveEntry>();

        foreach (var raw in rawKeys)
        {
            var relative = StripPrefix(raw, root);
            var lc = relative.ToLowerInvariant();

            var classification = ClassifyEntry(lc);
            entries.Add(new LspdfrArchiveEntry
            {
                ArchivePath = raw,
                RelativePath = relative,
                Classification = classification,
            });
        }

        static bool Usable(LspdfrArchiveEntry e) =>
            e.Classification != LspdfrEntryClassification.Ignored &&
            e.Classification != LspdfrEntryClassification.Unsafe;

        var missingRequired = RequiredPaths
            .Where(r => !entries.Any(e =>
                Usable(e) && e.RelativePath.Equals(r, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // LSPDFR core: any tolerated candidate satisfies it; report the canonical name when absent.
        var hasCore = entries.Any(e =>
            Usable(e) && LspdfrCorePathsLower.Contains(e.RelativePath.ToLowerInvariant()));
        if (!hasCore)
            missingRequired.Add(CanonicalLspdfrCore);

        return new LspdfrArchiveManifest
        {
            DetectedArchiveRoot = root,
            Entries = entries,
            MissingRequiredPaths = missingRequired,
        };
    }

    /// <summary>
    /// Validates the GTA V folder after an LSPDFR install and returns a structured result.
    /// </summary>
    public static LspdfrPostInstallValidation ValidatePostInstall(string gtaPath)
    {
        var missing = new List<string>();
        var doubleNested = new List<string>();

        foreach (var rel in RequiredPaths)
        {
            var full = Path.Combine(gtaPath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                missing.Add(rel);

            // Detect double-nesting: e.g. GTA/LSPDFR/RAGEPluginHook.exe
            var doubleNestedPath = Path.Combine(gtaPath, "LSPDFR", rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(doubleNestedPath))
                doubleNested.Add($"{rel} found nested under LSPDFR/ — move to GTA V root");
        }

        // LSPDFR core: satisfied by any tolerated candidate; report canonical name if none present.
        if (!LspdfrCorePaths.Any(c =>
                File.Exists(Path.Combine(gtaPath, c.Replace('/', Path.DirectorySeparatorChar)))))
            missing.Add(CanonicalLspdfrCore);

        foreach (var c in LspdfrCorePaths)
        {
            var doubleNestedCore = Path.Combine(gtaPath, "LSPDFR", c.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(doubleNestedCore))
            {
                doubleNested.Add($"{c} found nested under LSPDFR/ — move to GTA V root");
                break;
            }
        }

        foreach (var rel in RequiredDirectories)
        {
            var full = Path.Combine(gtaPath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full) || !Directory.EnumerateFileSystemEntries(full).Any())
                missing.Add(rel + "/");
        }

        var rageLog = ReadRageLog(gtaPath);

        return new LspdfrPostInstallValidation
        {
            MissingPaths = missing,
            DoubleNestedPaths = doubleNested,
            RageLogAnalysis = rageLog,
            IsValid = missing.Count == 0 && doubleNested.Count == 0 && !rageLog.HasCriticalErrors,
        };
    }

    // ── Archive root detection ────────────────────────────────────────────────

    /// <summary>
    /// If all entries share a single top-level directory that contains LSPDFR signature files,
    /// returns that prefix so it can be stripped. Returns empty string if no stripping is needed.
    /// </summary>
    internal static string DetectArchiveRoot(IEnumerable<string> normalizedKeys)
    {
        var keys = normalizedKeys.ToList();
        if (keys.Count == 0) return "";

        // Find the common first-level prefix
        var firstParts = keys
            .Select(k => k.Contains('/') ? k[..k.IndexOf('/', StringComparison.Ordinal)] : "")
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Only strip if there's exactly one top-level folder and ALL entries share it
        if (firstParts.Count != 1) return "";

        var prefix = firstParts[0] + "/";

        // Every entry must start with the prefix — if any don't, it's not a nested root
        if (!keys.All(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return "";

        var stripped = keys.Select(k => StripPrefix(k, prefix)).ToList();

        // Verify the stripped paths contain LSPDFR signature files
        if (stripped.Any(k => IsSignatureEntry(k.ToLowerInvariant())))
            return prefix;

        return "";
    }

    private static List<string> StripArchiveRoot(IEnumerable<string> keys)
    {
        var list = keys.ToList();
        var root = DetectArchiveRoot(list);
        return list.Select(k => StripPrefix(k, root)).ToList();
    }

    private static string StripPrefix(string path, string prefix) =>
        !string.IsNullOrEmpty(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;

    // ── Entry classification ──────────────────────────────────────────────────

    private static LspdfrEntryClassification ClassifyEntry(string normalizedLower)
    {
        if (InstallerSafetyPolicy.IsJunkEntry(normalizedLower))
            return LspdfrEntryClassification.Ignored;

        // Path traversal or absolute paths
        if (normalizedLower.Contains("..") || Path.IsPathRooted(normalizedLower))
            return LspdfrEntryClassification.Unsafe;

        // Executables outside known LSPDFR/RAGE locations are suspicious
        if (normalizedLower.EndsWith(".exe") &&
            !IsKnownRageExecutable(normalizedLower) &&
            !normalizedLower.StartsWith("plugins/", StringComparison.Ordinal))
            return LspdfrEntryClassification.NeedsReview;

        if (IsRequired(normalizedLower))
            return LspdfrEntryClassification.Required;

        if (IsKnownOptional(normalizedLower))
            return LspdfrEntryClassification.Optional;

        return LspdfrEntryClassification.NeedsReview;
    }

    private static bool IsRequired(string lc) =>
        lc is "ragepluginhook.exe" or "ragepluginhook.dll" ||
        LspdfrCorePathsLower.Contains(lc) ||
        lc.StartsWith("plugins/lspdfr/", StringComparison.Ordinal) ||
        lc.StartsWith("lspdfr/", StringComparison.Ordinal);

    private static bool IsKnownOptional(string lc) =>
        lc is "scripthookv.dll" or "dinput8.dll" or "scripthookvdotnet.asi" ||
        lc.StartsWith("plugins/", StringComparison.Ordinal) ||
        lc.StartsWith("scripts/", StringComparison.Ordinal) ||
        lc.StartsWith("els/", StringComparison.Ordinal);

    private static bool IsKnownRageExecutable(string lc) =>
        lc is "ragepluginhook.exe" or "gtavlauncherbypass.exe";

    // ── RAGE log parsing ──────────────────────────────────────────────────────

    private static RageLogAnalysis ReadRageLog(string gtaPath)
    {
        var logPath = Path.Combine(gtaPath, "RagePluginHook.log");
        if (!File.Exists(logPath))
            return new RageLogAnalysis { LogFound = false };

        try
        {
            // Read last 200 lines to avoid huge log overhead
            var lines = ReadLastLines(logPath, 200);
            var errors = new List<string>();

            foreach (var line in lines)
            {
                var lc = line.ToLowerInvariant();
                if (lc.Contains("error") || lc.Contains("failed") || lc.Contains("missing") ||
                    lc.Contains("not found") || lc.Contains("blocked") || lc.Contains("cannot"))
                {
                    errors.Add(line.Trim());
                }
            }

            return new RageLogAnalysis
            {
                LogFound = true,
                LogPath = logPath,
                RecentErrors = errors.Take(20).ToList(),
                HasCriticalErrors = errors.Count > 0,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"[LSPDFR_INSTALL] Could not read RAGE log: {ex.Message}");
            return new RageLogAnalysis { LogFound = false };
        }
    }

    private static List<string> ReadLastLines(string path, int count)
    {
        var lines = new List<string>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        var all = reader.ReadToEnd().Split('\n');
        return all.TakeLast(count).ToList();
    }
}

// ── Domain types ─────────────────────────────────────────────────────────────

public sealed class LspdfrArchiveManifest
{
    public string DetectedArchiveRoot { get; init; } = "";
    public List<LspdfrArchiveEntry> Entries { get; init; } = [];
    public List<string> MissingRequiredPaths { get; init; } = [];

    public bool IsComplete => MissingRequiredPaths.Count == 0;
    public IEnumerable<LspdfrArchiveEntry> Required => Entries.Where(e => e.Classification == LspdfrEntryClassification.Required);
    public IEnumerable<LspdfrArchiveEntry> Ignored => Entries.Where(e => e.Classification == LspdfrEntryClassification.Ignored);
    public IEnumerable<LspdfrArchiveEntry> NeedsReview => Entries.Where(e => e.Classification == LspdfrEntryClassification.NeedsReview);
    public IEnumerable<LspdfrArchiveEntry> Unsafe => Entries.Where(e => e.Classification == LspdfrEntryClassification.Unsafe);
}

public sealed class LspdfrArchiveEntry
{
    public string ArchivePath { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public LspdfrEntryClassification Classification { get; init; }
}

public enum LspdfrEntryClassification
{
    Required,
    Optional,
    Ignored,
    NeedsReview,
    Unsafe,
}

public sealed class LspdfrPostInstallValidation
{
    public bool IsValid { get; init; }
    public List<string> MissingPaths { get; init; } = [];
    public List<string> DoubleNestedPaths { get; init; } = [];
    public RageLogAnalysis RageLogAnalysis { get; init; } = new();
}

public sealed class RageLogAnalysis
{
    public bool LogFound { get; init; }
    public string? LogPath { get; init; }
    public bool HasCriticalErrors { get; init; }
    public List<string> RecentErrors { get; init; } = [];
}
