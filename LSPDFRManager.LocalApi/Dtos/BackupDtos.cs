namespace LSPDFRManager.LocalApi.Dtos;

public record BackupFileDto(
    string FileName,
    string FilePath,
    long SizeBytes,
    string SizeDisplay,
    DateTime LastWriteUtc);

public record BackupsListResponse(IReadOnlyList<BackupFileDto> Backups);

public record CreateBackupResponse(string JobId);

public record RestoreBackupRequest(string FileName);

public record RestoreBackupResponse(string JobId);

public record DeleteBackupRequest(string FileName);
