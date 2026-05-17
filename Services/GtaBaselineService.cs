using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class GtaBaselineService
{
    private static readonly Lazy<GtaBaselineService> LazyInstance =
        new(static () => new GtaBaselineService());

    public static GtaBaselineService Instance => LazyInstance.Value;

    private string FilePath => Path.Combine(AppDataPaths.Root, "gta_baseline.json");

    public GtaBaseline? Current { get; private set; }

    private GtaBaselineService() => Load();

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<GtaBaseline>(File.ReadAllText(FilePath));
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"[BASELINE] Failed to load gta_baseline.json: {ex.Message}");
        }
    }

    public void Save(GtaBaseline baseline)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }));
            Current = baseline;
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"[BASELINE] Failed to save gta_baseline.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures the current enabled-plugin list and config hashes as the known-good state.
    /// Merges with any existing GTA version data already in <see cref="Current"/>.
    /// </summary>
    public void MarkKnownGood()
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var enabledPaths = ModLibraryService.Instance.Mods
            .Where(m => m.IsEnabled)
            .SelectMany(m => m.InstalledFiles)
            .ToList();

        var configHashes = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(gtaPath) && Directory.Exists(gtaPath))
        {
            foreach (var ext in new[] { "*.ini", "*.xml", "*.cfg", "*.json" })
            {
                foreach (var file in Directory.EnumerateFiles(gtaPath, ext, SearchOption.AllDirectories))
                {
                    try
                    {
                        var rel = Path.GetRelativePath(gtaPath, file);
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        using var stream = File.OpenRead(file);
                        configHashes[rel] = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
                    }
                    catch { /* skip locked/inaccessible */ }
                }
            }
        }

        var updated = new GtaBaseline
        {
            GtaVersion             = Current?.GtaVersion,
            GtaHash                = Current?.GtaHash,
            GtaExeFileSizeBytes    = Current?.GtaExeFileSizeBytes,
            GtaExeLastWriteTimeUtc = Current?.GtaExeLastWriteTimeUtc,
            CheckedAtUtc           = Current?.CheckedAtUtc ?? DateTime.UtcNow,
            MarkedKnownGoodAt      = DateTime.UtcNow,
            EnabledPluginPaths     = enabledPaths,
            ConfigHashes           = configHashes,
        };
        Save(updated);
    }

    /// <summary>
    /// Compares current state to the saved known-good baseline.
    /// Returns null if no known-good snapshot exists.
    /// </summary>
    public KnownGoodDiff? DiffCurrentVsKnownGood()
    {
        if (Current?.MarkedKnownGoodAt is null) return null;

        var currentPlugins = new HashSet<string>(
            ModLibraryService.Instance.Mods
                .Where(m => m.IsEnabled)
                .SelectMany(m => m.InstalledFiles),
            StringComparer.OrdinalIgnoreCase);

        var baselinePlugins = new HashSet<string>(
            Current.EnabledPluginPaths, StringComparer.OrdinalIgnoreCase);

        var added   = currentPlugins.Except(baselinePlugins).ToList();
        var removed = baselinePlugins.Except(currentPlugins).ToList();

        var changedConfigs = new List<string>();
        var gtaPath = AppConfig.Instance.GtaPath;
        if (!string.IsNullOrEmpty(gtaPath) && Directory.Exists(gtaPath))
        {
            foreach (var (relPath, baselineHash) in Current.ConfigHashes)
            {
                var full = Path.Combine(gtaPath, relPath);
                if (!File.Exists(full))
                {
                    changedConfigs.Add($"{relPath} (deleted)");
                    continue;
                }
                try
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    using var stream = File.OpenRead(full);
                    var currentHash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
                    if (currentHash != baselineHash)
                        changedConfigs.Add(relPath);
                }
                catch { /* skip */ }
            }
        }

        return new KnownGoodDiff(added, removed, changedConfigs,
            added.Count > 0 || removed.Count > 0 || changedConfigs.Count > 0);
    }

    internal void Reset()
    {
        Current = null;
        Load();
    }
}
