using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModDuplicateDetector
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public InstalledMod? FindExactDuplicate(string incomingName)
    {
        if (string.IsNullOrWhiteSpace(incomingName))
            return null;

        return _library.Mods.FirstOrDefault(mod =>
            mod.Name.Equals(incomingName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
