namespace LSPDFRManager.Domain;

/// <summary>
/// Persisted record of a mod that has been installed into the GTA V directory.
/// Stored in the mod library (<c>library.json</c>) and managed by
/// <see cref="LSPDFRManager.Services.ModLibraryService"/>.
/// </summary>
public class InstalledMod
{
    /// <summary>Unique identifier for this installation record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Links this mod to its <see cref="InstallTransaction"/> for rollback. Null for mods installed before transaction tracking.</summary>
    public Guid? TransactionId { get; set; }

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

    /// <summary>Lower values load first when the user selects load-order sorting.</summary>
    public int LoadOrderPriority { get; set; }

    /// <summary>Whether this mod has file conflicts with other installed mods.</summary>
    public bool HasConflict { get; set; }

    /// <summary>Runtime property indicating if installation is in progress.</summary>
    public bool IsInstalling { get; set; }

    /// <summary>Detection confidence score (0–100) from installation; defaults to 100 for already-installed mods.</summary>
    public int DetectionScore { get; set; } = 100;

    /// <summary>User-provided notes for this mod.</summary>
    public string Notes { get; set; } = "";

    /// <summary>Primary mod image URL, typically from lcpdfr page metadata.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Thumbnail URL used for compact card/list rendering.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Whether the user has marked this mod as a favourite.</summary>
    public bool IsFavorite { get; set; } = false;

    /// <summary>Total size in bytes of all installed files.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>Human-readable size string (e.g. "12.3 MB"). Empty when size is unknown.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TotalSizeDisplay
    {
        get
        {
            if (TotalSizeBytes <= 0) return "";
            string[] sizes = ["B", "KB", "MB", "GB"];
            double len = TotalSizeBytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
