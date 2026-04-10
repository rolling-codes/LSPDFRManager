namespace LSPDFRManager.Models;

/// <summary>
/// Persisted record of a mod that has been installed into the GTA V directory.
/// Stored in the mod library (<c>library.json</c>) and managed by
/// <see cref="LSPDFRManager.Services.ModLibraryService"/>.
/// </summary>
public class InstalledMod
{
    /// <summary>Unique identifier for this installation record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name shown in the Library.</summary>
    public string Name { get; set; } = "";

    /// <summary>Broad mod category.</summary>
    public ModType Type { get; set; }

    /// <summary>Human-readable label for <see cref="Type"/>.</summary>
    public string TypeLabel { get; set; } = "";

    /// <summary>Hex colour string used for the type badge in the UI.</summary>
    public string TypeColor { get; set; } = "#6B7280";

    /// <summary>Version string (e.g. "8.4.5").</summary>
    public string Version { get; set; } = "";

    /// <summary>Mod author, as entered by the user during installation.</summary>
    public string Author { get; set; } = "";

    /// <summary>Path to the original source archive that was installed.</summary>
    public string SourcePath { get; set; } = "";

    /// <summary>Root directory into which the mod was extracted (GTA V folder).</summary>
    public string InstallPath { get; set; } = "";

    /// <summary>DLC pack name, applicable to <see cref="ModType.VehicleDlc"/> mods.</summary>
    public string DlcPackName { get; set; } = "";

    /// <summary>
    /// Whether the mod is currently active.  When <c>false</c>, installed files
    /// have been renamed with a <c>.disabled</c> suffix so GTA V ignores them.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Absolute paths of every file written during installation.</summary>
    public List<string> InstalledFiles { get; set; } = [];

    /// <summary>UTC timestamp of when the mod was installed.</summary>
    public DateTime InstalledAt { get; set; } = DateTime.Now;
}
