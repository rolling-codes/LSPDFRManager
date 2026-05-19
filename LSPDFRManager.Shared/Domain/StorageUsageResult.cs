namespace LSPDFRManager.Domain;

public class StorageUsageResult
{
    public string FolderPath { get; init; } = "";
    public string Label { get; init; } = "";
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
    public string SizeDisplay => SizeBytes < 1024 * 1024
        ? $"{SizeBytes / 1024.0:F1} KB"
        : $"{SizeBytes / (1024.0 * 1024):F1} MB";
}
