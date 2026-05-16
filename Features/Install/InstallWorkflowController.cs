using LSPDFRManager.Core;
using LSPDFRManager.Core.Commands;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.Features.Install;

public sealed class InstallWorkflowController : IInstallController
{
    private readonly ModDetector _detector;
    private readonly InstallQueue _queue;

    public InstallWorkflowController(ModDetector? detector = null, InstallQueue? queue = null)
    {
        _detector = detector ?? new ModDetector();
        _queue = queue ?? InstallQueue.Instance;
        Commands = new Dictionary<string, IAppCommand>();
    }

    public string FeatureKey => "Install";

    public IReadOnlyDictionary<string, IAppCommand> Commands { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ModInfo> DetectAsync(
        string path,
        string? nameOverride = null,
        string? authorOverride = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detected = _detector.Detect(path);
            detected.Name = string.IsNullOrWhiteSpace(nameOverride) ? detected.Name : nameOverride;
            detected.Author = string.IsNullOrWhiteSpace(authorOverride) ? detected.Author : authorOverride;
            return detected;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ModInfo>> DetectBatchAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var validPaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var tasks = validPaths.Select(path => DetectAsync(path, cancellationToken: cancellationToken));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<ModInfo> StageBrowseDownloadAsync(
        string localPath,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var mod = await DetectAsync(localPath, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(mod.Name) || mod.Name.StartsWith("Mod "))
            mod.Name = CleanDisplayName(displayName);

        return mod;
    }

    public Task<InstallPlan> BuildReviewPlanAsync(
        ModInfo mod,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mod.Type == ModType.LspdfrPlugin)
            {
                var manifest = LspdfrInstallService.TryInspectArchive(mod.SourcePath);
                if (manifest is not null)
                    mod.ArchiveRootPrefix = manifest.DetectedArchiveRoot;
            }

            return new SmartInstallPlanner().BuildPlan(mod.SourcePath);
        }, cancellationToken);
    }

    public Task<ConfirmedInstall> ConfirmInstallAsync(
        ModInfo mod,
        string gtaPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isLspdfrCore = mod.Type == ModType.LspdfrPlugin &&
                           !string.IsNullOrEmpty(mod.ArchiveRootPrefix);

        _queue.Enqueue(mod);

        return Task.FromResult(new ConfirmedInstall(
            isLspdfrCore,
            isLspdfrCore ? mod.Name : null,
            gtaPath));
    }

    private static string CleanDisplayName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        stem = System.Text.RegularExpressions.Regex.Replace(stem, @"[_\-\.]+", " ");
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(stem.Trim().ToLowerInvariant());
    }
}
