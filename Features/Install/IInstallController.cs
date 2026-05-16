using LSPDFRManager.Core.Features;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Features.Install;

public interface IInstallController : IFeatureController
{
    Task<ModInfo> DetectAsync(
        string path,
        string? nameOverride = null,
        string? authorOverride = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModInfo>> DetectBatchAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default);

    Task<ModInfo> StageBrowseDownloadAsync(
        string localPath,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<InstallPlan> BuildReviewPlanAsync(
        ModInfo mod,
        CancellationToken cancellationToken = default);

    Task<ConfirmedInstall> ConfirmInstallAsync(
        ModInfo mod,
        string gtaPath,
        CancellationToken cancellationToken = default);
}
