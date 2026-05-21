namespace LSPDFRManager.Features.Install;

public sealed record ConfirmedInstall(
    bool RequiresLspdfrPostInstallCheck,
    string? PostInstallModName,
    string GtaPath);
