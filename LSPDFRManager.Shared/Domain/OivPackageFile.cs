namespace LSPDFRManager.Domain;

/// <summary>
/// Represents one source file staged for inclusion in an OIV package.
/// SourcePath is an absolute path on disk; InstallPath is the relative
/// path that will appear under content/ in the assembled ZIP.
/// </summary>
public sealed record OivPackageFile(
    string SourcePath,
    string InstallPath,
    long   SizeBytes);
