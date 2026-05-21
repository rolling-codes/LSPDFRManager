using System.Threading.Tasks;
using LSPDFRManager.Core.Commands;
using LSPDFRManager.Core.Features;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;

namespace LSPDFRManager.Features.Updates;

public sealed class UpdateWorkflowController : IUpdateController, IFeatureController
{
    private readonly UpdateCheckService _updateService;

    public string FeatureKey => "Updates";

    public IReadOnlyDictionary<string, IAppCommand> Commands => new Dictionary<string, IAppCommand>();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public UpdateWorkflowController(UpdateCheckService? updateService = null)
    {
        // Currently falling back to manual instantiation to match other services,
        // but ready for DI injection when DI composition is updated.
        _updateService = updateService ?? new UpdateCheckService();
    }

    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
    {
        return _updateService.CheckAsync(ct);
    }
}
