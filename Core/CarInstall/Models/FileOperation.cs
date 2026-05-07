namespace LSPDFRManager.OpenIv.CarInstall.Models;

public class FileOperation
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public bool Overwrite { get; init; }
}
