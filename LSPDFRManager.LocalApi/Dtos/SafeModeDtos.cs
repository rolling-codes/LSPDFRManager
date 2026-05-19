namespace LSPDFRManager.LocalApi.Dtos;

public record EmergencyRecoveryActionDto(
    string Description,
    string AffectedPath,
    bool WillDisable);

public record EmergencyRecoveryPlanDto(
    string Mode,
    IReadOnlyList<EmergencyRecoveryActionDto> Actions,
    DateTime CreatedAt);

public record SafeModeApplyResponse(
    bool Success,
    string? Error,
    int FilesDisabled);
