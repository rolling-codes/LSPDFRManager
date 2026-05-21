using LSPDFRManager.Domain;
using LSPDFRManager.LocalApi.Dtos;
using LSPDFRManager.Services;

namespace LSPDFRManager.LocalApi.Endpoints;

public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfig(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/config", GetConfig);
        app.MapPut("/api/v1/config", PutConfig);
        app.MapPost("/api/v1/config/validate-gta-path", ValidateGtaPath);
        return app;
    }

    private static IResult GetConfig()
    {
        var cfg = AppConfig.Instance;
        return Results.Ok(ToDto(cfg));
    }

    private static IResult PutConfig(UpdateConfigRequest req)
    {
        if (req is null)
            return Results.BadRequest(new { error = "Request body is required." });

        var cfg = AppConfig.Instance;

        if (req.GtaPath is not null)
            cfg.GtaPath = req.GtaPath;

        if (req.BackupPath is not null)
            cfg.BackupPath = req.BackupPath;

        if (req.AutoBackupOnInstall.HasValue)
            cfg.AutoBackupOnInstall = req.AutoBackupOnInstall.Value;

        if (req.ConfirmBeforeUninstall.HasValue)
            cfg.ConfirmBeforeUninstall = req.ConfirmBeforeUninstall.Value;

        if (req.AutoLaunchAfterInstall.HasValue)
            cfg.AutoLaunchAfterInstall = req.AutoLaunchAfterInstall.Value;

        if (req.AutoInstallHighConfidence.HasValue)
            cfg.AutoInstallHighConfidence = req.AutoInstallHighConfidence.Value;

        if (req.DeleteTempAfterInstall.HasValue)
            cfg.DeleteTempAfterInstall = req.DeleteTempAfterInstall.Value;

        if (req.MaxInstallLogEntries.HasValue)
        {
            if (req.MaxInstallLogEntries.Value < 1 || req.MaxInstallLogEntries.Value > 10000)
                return Results.BadRequest(new { error = "MaxInstallLogEntries must be between 1 and 10000." });
            cfg.MaxInstallLogEntries = req.MaxInstallLogEntries.Value;
        }

        if (req.MinimumFreeDiskSpaceMb.HasValue)
        {
            if (req.MinimumFreeDiskSpaceMb.Value < 0)
                return Results.BadRequest(new { error = "MinimumFreeDiskSpaceMb must be >= 0." });
            cfg.MinimumFreeDiskSpaceMb = req.MinimumFreeDiskSpaceMb.Value;
        }

        if (req.AutoStartBrowseApi.HasValue)
            cfg.AutoStartBrowseApi = req.AutoStartBrowseApi.Value;

        if (req.BrowseApiPath is not null)
            cfg.BrowseApiPath = string.IsNullOrWhiteSpace(req.BrowseApiPath) ? null : req.BrowseApiPath;

        if (req.BrowseApiBaseUrl is not null)
        {
            if (!Uri.TryCreate(req.BrowseApiBaseUrl, UriKind.Absolute, out _))
                return Results.BadRequest(new { error = "BrowseApiBaseUrl is not a valid absolute URL." });
            cfg.BrowseApiBaseUrl = req.BrowseApiBaseUrl;
        }

        if (req.AutoBackupEnabled.HasValue)
            cfg.AutoBackupEnabled = req.AutoBackupEnabled.Value;

        if (req.BackupScheduleMode is not null)
        {
            if (!Enum.TryParse<BackupScheduleMode>(req.BackupScheduleMode, ignoreCase: true, out var mode))
                return Results.BadRequest(new { error = $"Invalid BackupScheduleMode: {req.BackupScheduleMode}" });
            cfg.BackupScheduleMode = mode;
        }

        if (req.MaxBackupCount.HasValue)
        {
            if (req.MaxBackupCount.Value < 1 || req.MaxBackupCount.Value > 100)
                return Results.BadRequest(new { error = "MaxBackupCount must be between 1 and 100." });
            cfg.MaxBackupCount = req.MaxBackupCount.Value;
        }

        if (req.CompressBackups.HasValue)
            cfg.CompressBackups = req.CompressBackups.Value;

        if (req.ShowSetupWizardOnStartup.HasValue)
            cfg.ShowSetupWizardOnStartup = req.ShowSetupWizardOnStartup.Value;

        if (req.CheckForUpdatesOnStartup.HasValue)
            cfg.CheckForUpdatesOnStartup = req.CheckForUpdatesOnStartup.Value;

        if (req.UiScale.HasValue)
        {
            if (req.UiScale.Value < 0.5 || req.UiScale.Value > 3.0)
                return Results.BadRequest(new { error = "UiScale must be between 0.5 and 3.0." });
            cfg.UiScale = req.UiScale.Value;
        }

        cfg.Save();
        return Results.Ok(ToDto(cfg));
    }

    private static IResult ValidateGtaPath(ValidateGtaPathRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Path))
            return Results.BadRequest(new { error = "Path is required." });

        if (!Directory.Exists(req.Path))
            return Results.Ok(new ValidateGtaPathResponse(false, "Folder does not exist."));

        if (LspdfrInstallLocator.FindGtaExe(req.Path) is null)
            return Results.Ok(new ValidateGtaPathResponse(false, "GTA V executable not found in this folder."));

        return Results.Ok(new ValidateGtaPathResponse(true, null));
    }

    private static AppConfigDto ToDto(AppConfig cfg) => new(
        GtaPath: cfg.GtaPath,
        BackupPath: cfg.BackupPath,
        AutoBackupOnInstall: cfg.AutoBackupOnInstall,
        ConfirmBeforeUninstall: cfg.ConfirmBeforeUninstall,
        AutoLaunchAfterInstall: cfg.AutoLaunchAfterInstall,
        AutoInstallHighConfidence: cfg.AutoInstallHighConfidence,
        DeleteTempAfterInstall: cfg.DeleteTempAfterInstall,
        MaxInstallLogEntries: cfg.MaxInstallLogEntries,
        MinimumFreeDiskSpaceMb: cfg.MinimumFreeDiskSpaceMb,
        AutoStartBrowseApi: cfg.AutoStartBrowseApi,
        BrowseApiPath: cfg.BrowseApiPath,
        BrowseApiBaseUrl: cfg.BrowseApiBaseUrl,
        AutoBackupEnabled: cfg.AutoBackupEnabled,
        BackupScheduleMode: cfg.BackupScheduleMode.ToString(),
        MaxBackupCount: cfg.MaxBackupCount,
        CompressBackups: cfg.CompressBackups,
        ShowSetupWizardOnStartup: cfg.ShowSetupWizardOnStartup,
        CheckForUpdatesOnStartup: cfg.CheckForUpdatesOnStartup,
        UiScale: cfg.UiScale
    );
}
