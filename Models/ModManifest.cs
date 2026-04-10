namespace LSPDFRManager.Models;

/// <summary>
/// Serialisable snapshot of a mod library, produced by
/// <see cref="LSPDFRManager.Services.ExportService"/> and consumed by
/// <see cref="LSPDFRManager.Services.BatchReinstallService"/>.
/// Can be saved as a plain JSON <c>.lspmanifest</c> file or bundled with
/// mod archives inside a ZIP.
/// </summary>
public class ModManifest
{
    /// <summary>Schema version of this manifest format.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>UTC time the manifest was created.</summary>
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>One entry per mod that was installed when the manifest was exported.</summary>
    public List<ManifestEntry> Mods { get; set; } = [];
}

/// <summary>
/// Serialised representation of a single <see cref="InstalledMod"/> inside a
/// <see cref="ModManifest"/>, including captured config file snapshots.
/// </summary>
public class ManifestEntry
{
    /// <summary>Original <see cref="InstalledMod.Id"/> at export time.</summary>
    public Guid OriginalId { get; set; }

    public string Name { get; set; } = "";
    public ModType Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";

    /// <summary>Path to the source archive to reinstall from.</summary>
    public string SourceArchivePath { get; set; } = "";

    public string DlcPackName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    /// <summary>Relative (to GTA V root) paths of files written during the original install.</summary>
    public List<string> InstalledFiles { get; set; } = [];

    /// <summary>Config file snapshots captured at export time.</summary>
    public List<ConfigSnapshot> Configs { get; set; } = [];
}

/// <summary>
/// A point-in-time snapshot of a single mod config file, embedded in a
/// <see cref="ManifestEntry"/> so it can be restored during reinstallation.
/// </summary>
public class ConfigSnapshot
{
    /// <summary>Config file name (e.g. <c>config.xml</c>).</summary>
    public string FileName { get; set; } = "";

    /// <summary>Full text content of the config file.</summary>
    public string Content { get; set; } = "";

    /// <summary>Path relative to the GTA V root where the file should be restored.</summary>
    public string RelativeInstallPath { get; set; } = "";
}
