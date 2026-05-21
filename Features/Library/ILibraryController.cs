using LSPDFRManager.Core.Features;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Library.Models;

namespace LSPDFRManager.Features.Library;

public interface ILibraryController : IFeatureController
{
    IReadOnlyList<BulkToggleState> SetVisibleModsEnabled(IEnumerable<InstalledMod> visibleMods, bool enabled);

    void UndoBulkToggle(IEnumerable<BulkToggleState> snapshot);
}
