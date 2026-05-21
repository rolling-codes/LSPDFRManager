using LSPDFRManager.Core.Commands;

namespace LSPDFRManager.Core.Features;

public interface IFeatureController
{
    string FeatureKey { get; }

    IReadOnlyDictionary<string, IAppCommand> Commands { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
