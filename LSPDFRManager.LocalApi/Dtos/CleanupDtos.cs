namespace LSPDFRManager.LocalApi.Dtos;

public record RemovalCandidateDto(
    string RelativePath,
    string Classification,
    string RiskLevel,
    long SizeBytes,
    string SizeDisplay,
    bool IsBlocked);

public record CleanupScanResponse(
    IReadOnlyList<RemovalCandidateDto> Candidates,
    long TotalSizeBytes);

public record CleanupApplyRequest(
    IReadOnlyList<string> RelativePaths,
    string Mode);

public record CleanupApplyResponse(
    bool Success,
    int FilesDeleted,
    long BytesFreed,
    string? Error);
