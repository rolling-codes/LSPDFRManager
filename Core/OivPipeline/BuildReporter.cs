namespace LSPDFRManager.OivPipeline;

using System.Text.Json;
using System.Text.Json.Serialization;
using LSPDFRManager.OivPipeline.Models;

public static class BuildReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string WriteReport(string outputDir, BuildReport report)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, "build_report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        return path;
    }
}
