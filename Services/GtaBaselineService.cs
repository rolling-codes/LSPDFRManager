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

    internal void Reset()
    {
        Current = null;
        Load();
    }
}
