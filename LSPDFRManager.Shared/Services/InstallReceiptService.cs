using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Generates a human-readable install receipt from an <see cref="InstallTransaction"/>.
/// Supports plain text and JSON formats.
/// </summary>
public sealed class InstallReceiptService
{
    public string GenerateText(InstallTransaction tx)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine("  LSPDFR Manager — Install Receipt");
        sb.AppendLine("═══════════════════════════════════════════════════════");
        sb.AppendLine($"  Mod Name   : {tx.ModName}");
        sb.AppendLine($"  Mod ID     : {tx.ModId}");
        sb.AppendLine($"  Installed  : {tx.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Status     : {tx.State}");
        sb.AppendLine();

        if (tx.FilesAdded.Count > 0)
        {
            sb.AppendLine($"  Files Added ({tx.FilesAdded.Count}):");
            foreach (var f in tx.FilesAdded)
                sb.AppendLine($"    + {f.DestinationPath}");
            sb.AppendLine();
        }

        if (tx.FilesOverwritten.Count > 0)
        {
            sb.AppendLine($"  Files Overwritten ({tx.FilesOverwritten.Count}):");
            foreach (var f in tx.FilesOverwritten)
                sb.AppendLine($"    ~ {f.DestinationPath}");
            sb.AppendLine();
        }

        if (tx.WasDlcEntry && tx.DlcPackName is not null)
        {
            sb.AppendLine($"  DLC Entry Added: {tx.DlcPackName}");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════");
        return sb.ToString();
    }

    public string GenerateJson(InstallTransaction tx)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                tx.Id, tx.ModName, tx.ModId,
                InstalledAt = tx.Timestamp,
                tx.State,
                FilesAdded = tx.FilesAdded.Select(f => f.DestinationPath).ToList(),
                FilesOverwritten = tx.FilesOverwritten.Select(f => f.DestinationPath).ToList(),
                tx.WasDlcEntry, tx.DlcPackName,
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public async Task ExportAsync(InstallTransaction tx, string outputPath)
    {
        var text = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? GenerateJson(tx)
            : GenerateText(tx);
        await File.WriteAllTextAsync(outputPath, text);
    }
}
