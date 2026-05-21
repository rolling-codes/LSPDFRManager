using System.Threading;
using System.Threading.Tasks;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Features.Updates;

public interface IUpdateController
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct);
}
