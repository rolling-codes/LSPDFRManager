namespace LSPDFRManager.Domain;

/// <summary>
/// Transient result produced by <see cref="LSPDFRManager.Services.ModDetector"/>
/// after inspecting an archive or directory.  Not persisted — converted to an
/// <see cref="InstalledMod"/> by <see cref="LSPDFRManager.Core.InstallQueue"/>
/// after a successful install.
/// </summary>
public class ModInfo
{
    /// <summary>Human-readable mod name, cleaned from the archive file name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Detected broad category.</summary>
    public ModType Type { get; set; }

    /// <summary>Display label for <see cref="Type"/> (e.g. "LSPDFR Plugin").</summary>
    public string TypeLabel { get; set; } = "";

    /// <summary>Hex colour string associated with <see cref="Type"/> for UI badges.</summary>
    public string TypeColor { get; set; } = "#6B7280";

    /// <summary>Full path to the source archive or directory.</summary>
    public string SourcePath { get; set; } = "";

    /// <summary>Relative file paths found inside the archive.</summary>
    public List<string> Files { get; set; } = [];

    /// <summary>Detection confidence in the range [0, 1].</summary>
    public float Confidence { get; set; }

    /// <summary>Human-readable label for <see cref="Confidence"/>.</summary>
    public string ConfidenceLabel => Confidence >= 0.75f ? "High" : Confidence >= 0.45f ? "Medium" : "Low";

    /// <summary>Version string extracted from the archive name, or <c>null</c> if not found.</summary>
    public string? Version { get; set; }

    /// <summary>Author name, optionally supplied by the user during installation.</summary>
    public string? Author { get; set; }

    /// <summary><c>true</c> for add-on DLC packs; <c>false</c> for replacements.</summary>
    public bool IsAddon { get; set; }

    /// <summary>DLC pack folder name extracted from the archive structure, if applicable.</summary>
    public string? DlcPackName { get; set; }

    /// <summary>Non-fatal warnings collected during detection (e.g. low confidence, missing DLC name).</summary>
    public List<string> Warnings { get; set; } = [];
}
