namespace LSPDFRManager.Domain;

public sealed class ModUninstallResult
{
    public List<string> DeletedFiles { get; } = [];
    public List<string> MissingFiles { get; } = [];
    public List<string> SkippedSharedFiles { get; } = [];
    public List<string> FailedFiles { get; } = [];
    public List<string> Errors { get; } = [];

    public bool Success => FailedFiles.Count == 0 && Errors.Count == 0;

    public string StatusMessage
    {
        get
        {
            if (Success)
                return "Mod uninstalled successfully.";

            var failedCount = FailedFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var errorCount = Errors.Count;

            if (failedCount > 0 && errorCount > 0)
                return $"Could not uninstall this mod because {failedCount} file(s) could not be deleted and {errorCount} cleanup error(s) occurred. The mod has been kept in your library.";

            if (failedCount > 0)
                return $"Could not uninstall this mod because {failedCount} file(s) could not be deleted. The mod has been kept in your library. Close GTA V or any tool using the files, then try again.";

            return $"Could not finish uninstalling this mod because {errorCount} cleanup error(s) occurred. The mod has been kept in your library.";
        }
    }

    public void AddDeleteFailure(string path)
    {
        FailedFiles.Add(path);
    }

    public static ModUninstallResult NotFound(Guid id)
    {
        var result = new ModUninstallResult();
        result.Errors.Add($"Installed mod record was not found: {id}");
        return result;
    }
}
