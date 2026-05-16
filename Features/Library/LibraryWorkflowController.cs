using LSPDFRManager.Core.Commands;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Library.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.Features.Library;

public sealed class LibraryWorkflowController : ILibraryController
{
    private readonly ModLibraryService _library;

    public LibraryWorkflowController(ModLibraryService? library = null)
    {
        _library = library ?? ModLibraryService.Instance;
        Commands = new Dictionary<string, IAppCommand>();
    }

    public string FeatureKey => "Library";

    public IReadOnlyDictionary<string, IAppCommand> Commands { get; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public IReadOnlyList<BulkToggleState> SetVisibleModsEnabled(IEnumerable<InstalledMod> visibleMods, bool enabled)
    {
        var targets = visibleMods
            .Where(mod => mod.IsEnabled != enabled)
            .ToList();

        var snapshot = targets
            .Select(mod => new BulkToggleState(mod.Id, mod.IsEnabled))
            .ToList();

        _library.SetEnabledBatch(targets.Select(mod => mod.Id), enabled);

        return snapshot;
    }

    public void UndoBulkToggle(IEnumerable<BulkToggleState> snapshot)
    {
        foreach (var item in snapshot)
            _library.SetEnabled(item.Id, item.WasEnabled);
    }
}
