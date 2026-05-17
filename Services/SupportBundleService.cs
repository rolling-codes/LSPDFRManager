using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Assembles a sanitized support bundle ZIP containing logs, diagnostics, library
/// state, and feature flags — no raw user paths, no secrets.
/// </summary>
public sealed class SupportBundleService
{
    private static readonly string[] SensitiveSubstrings = ["password", "secret", "token", "apikey", "credential"];

    /// <summary>
    /// Creates a support bundle ZIP at <paramref name="outputPath"/>.
    /// Returns the path of the created file.
    /// </summary>
    public async Task<string> ExportAsync(string outputPath, IProgress<string>? progress = null)
    {
        var zipPath = outputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? outputPath : outputPath + ".zip";

        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

        using var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

        // app-info.json
        progress?.Report("Collecting app info…");
        await AddJsonEntry(zip, "app-info.json", BuildAppInfo());

        // feature-flags.json
        progress?.Report("Collecting feature flags…");
        await AddJsonEntry(zip, "feature-flags.json", BuildFeatureFlags());

        // installed-mods.json (sanitized)
        progress?.Report("Collecting mod library…");
        await AddJsonEntry(zip, "installed-mods.json", BuildModLibrary());

        // diagnostic-events.log (app.log, sanitized)
        progress?.Report("Collecting app log…");
        AddLogEntry(zip, "diagnostic-events.log", AppDataPaths.LogFile);

        // change-history.json
        progress?.Report("Collecting change history…");
        await AddJsonEntry(zip, "change-history.json", BuildChangeHistory());

        // backup-history.json
        progress?.Report("Collecting backup history…");
        await AddJsonEntry(zip, "backup-history.json", BuildBackupHistory());

        // sanitized-paths.txt
        progress?.Report("Writing path info…");
        await AddTextEntry(zip, "sanitized-paths.txt", BuildSanitizedPaths());

        // recent-rph-logs/
        progress?.Report("Collecting RPH logs…");
        AddRecentLogs(zip, progress);

        progress?.Report("Done.");
        return zipPath;
    }

    // ── builders ─────────────────────────────────────────────────────────────

    private static object BuildAppInfo() => new
    {
        AppVersion    = GetAppVersion(),
        OsVersion     = Environment.OSVersion.VersionString,
        DotNetVersion = Environment.Version.ToString(),
        MachineName   = "REDACTED",
        GeneratedAt   = DateTimeOffset.UtcNow,
        GtaPathSanitized = SanitizePath(AppConfig.Instance.GtaPath),
    };

    private static object BuildFeatureFlags()
    {
        var svc = FeatureFlagService.Instance;
        return svc.AllFeatures.Select(f => new
        {
            f.Id, f.Title, f.Stage,
            Enabled = svc.IsEnabled(f.Id),
        }).ToList();
    }

    private static object BuildModLibrary()
    {
        return ModLibraryService.Instance.Mods.Select(m => new
        {
            m.Name, m.Type, m.IsEnabled, m.Version,
            m.Author, m.InstalledAt, m.Notes,
            FileCount = m.InstalledFiles.Count,
            // Sanitize install path
            InstallPath = SanitizePath(m.InstallPath),
        }).ToList();
    }

    private static object BuildChangeHistory()
    {
        return ChangeHistoryService.Instance.Entries.Take(200).Select(e => new
        {
            e.Action, e.Description, e.OccurredAt,
            AffectedFile = SanitizePath(e.AffectedFile),
        }).ToList();
    }

    private static object BuildBackupHistory()
    {
        var manifestPath = AppDataPaths.BackupManifestFile;
        if (!File.Exists(manifestPath)) return Array.Empty<object>();
        try
        {
            var json = File.ReadAllText(manifestPath);
            return System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? Array.Empty<object>();
        }
        catch { return Array.Empty<object>(); }
    }

    private static string BuildSanitizedPaths()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"GTA V Path: {SanitizePath(AppConfig.Instance.GtaPath)}");
        sb.AppendLine($"AppData Root: {SanitizePath(AppDataPaths.Root)}");
        return sb.ToString();
    }

    // ── ZIP helpers ───────────────────────────────────────────────────────────

    private static async Task AddJsonEntry(System.IO.Compression.ZipArchive zip, string entryName, object obj)
    {
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        var json = System.Text.Json.JsonSerializer.Serialize(obj,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
    }

    private static async Task AddTextEntry(System.IO.Compression.ZipArchive zip, string entryName, string text)
    {
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
    }

    private static void AddLogEntry(System.IO.Compression.ZipArchive zip, string entryName, string logPath)
    {
        if (!File.Exists(logPath)) return;
        try
        {
            // Read, sanitize, write — don't copy raw in case log contains full paths
            var lines = File.ReadAllLines(logPath);
            var sanitized = lines.Select(l => ContainsSensitive(l) ? "[REDACTED LINE]" : SanitizePath(l));
            var entry = zip.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            foreach (var line in sanitized.TakeLast(2000)) // last 2000 lines
                writer.WriteLine(line);
        }
        catch { /* best-effort */ }
    }

    private static void AddRecentLogs(System.IO.Compression.ZipArchive zip, IProgress<string>? progress)
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        if (string.IsNullOrEmpty(gtaPath) || !Directory.Exists(gtaPath)) return;

        var logNames = new[] { "RagePluginHook.log", "ScriptHookV.log", "ScriptHookVDotNet.log" };
        foreach (var name in logNames)
        {
            var full = Path.Combine(gtaPath, name);
            if (!File.Exists(full)) continue;
            progress?.Report($"Adding {name}…");
            AddLogEntry(zip, $"recent-rph-logs/{name}", full);
        }
    }

    // ── utilities ─────────────────────────────────────────────────────────────

    private static string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        // Replace home directory with %USERPROFILE%
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            path = "%USERPROFILE%" + path[home.Length..];
        // Replace AppData with %APPDATA%
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData) && path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            path = "%APPDATA%" + path[appData.Length..];
        return path;
    }

    private static bool ContainsSensitive(string line) =>
        SensitiveSubstrings.Any(s => line.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static string GetAppVersion()
    {
        try
        {
            return System.Reflection.Assembly.GetEntryAssembly()
                ?.GetName().Version?.ToString() ?? "unknown";
        }
        catch { return "unknown"; }
    }
}
