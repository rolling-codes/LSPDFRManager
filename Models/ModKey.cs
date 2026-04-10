namespace LSPDFRManager.Models;

/// <summary>
/// A stored mod license key file, managed by
/// <see cref="LSPDFRManager.Services.KeyManagerService"/>.
/// Key files are copied into the manager's keys directory so they survive
/// changes to the original source path.
/// </summary>
public class ModKey
{
    /// <summary>Unique identifier for this key record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the <see cref="InstalledMod"/> this key belongs to, or
    /// <see cref="Guid.Empty"/> if not yet associated.
    /// </summary>
    public Guid AssociatedModId { get; set; }

    /// <summary>Display name of the mod this key is for.</summary>
    public string ModName { get; set; } = "";

    /// <summary>File name to use when deploying the key (e.g. <c>mymod.key</c>).</summary>
    public string KeyFileName { get; set; } = "";

    /// <summary>Raw text content of the key file.</summary>
    public string KeyContent { get; set; } = "";

    /// <summary>
    /// Path to the manager's local copy of the key file
    /// (<c>%APPDATA%\LSPDFRManager\keys\{Id}_{KeyFileName}</c>).
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>When the key was added to the manager.</summary>
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
