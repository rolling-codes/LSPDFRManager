namespace LSPDFRManager.Domain;

/// <summary>
/// Immutable plan produced by OivSourceScanner and validated by OivPackageValidator.
/// Use `plan with { ... }` to create derived plans (it is a record).
/// </summary>
public sealed record OivPackagePlan
{
    public string           Name        { get; init; } = "";
    public string           Version     { get; init; } = "1.0";
    public string           Author      { get; init; } = "";
    public string           Description { get; init; } = "";
    public OivPackageKind   Kind        { get; init; } = OivPackageKind.Basic;

    public IReadOnlyList<OivPackageFile> Files    { get; init; } = [];
    public IReadOnlyList<string>         Errors   { get; init; } = [];
    public IReadOnlyList<string>         Warnings { get; init; } = [];

    public bool IsValid    => Errors.Count == 0;
    public long TotalBytes => Files.Sum(f => f.SizeBytes);

    public string TotalSizeLabel
    {
        get
        {
            var b = TotalBytes;
            if (b < 1_024)             return $"{b} B";
            if (b < 1_024 * 1_024)    return $"{b / 1_024.0:F1} KB";
            return                            $"{b / (1_024.0 * 1_024):F1} MB";
        }
    }
}
