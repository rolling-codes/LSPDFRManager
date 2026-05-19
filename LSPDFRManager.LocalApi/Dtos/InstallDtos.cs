namespace LSPDFRManager.LocalApi.Dtos;

public record StartInstallRequest(string SourcePath);

public record StartInstallResponse(string JobId);

public record InstallResultDto(
    bool Success,
    string? UserMessage,
    string? Error,
    int FilesInstalled);
