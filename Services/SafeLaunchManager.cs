using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class SafeLaunchManager
{
    private static readonly string[] EssentialFiles =
    [
        "GTA5.exe", "PlayGTAV.exe", "RAGEPluginHook.exe",
        "ScriptHookV.dll", "dinput8.dll",
        @"plugins\LSPDFR.dll", @"plugins\RageNativeUI.dll",
    ];

    private static readonly string[] OptionalAsiFolders = ["mods", "ELS"];
    private static readonly string[] ScriptExtensions = [".cs", ".vb", ".lua"];

    public SafeLaunchPlan BuildPlan(string mode)
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var changes = new List<SafeLaunchChange>();

        switch (mode)
        {
            case "LspdfrOnly":
                DisableNonEssential(gtaPath, changes, keepLspdfr: true);
                break;
            case "VanillaGtaV":
                DisableNonEssential(gtaPath, changes, keepLspdfr: false);
                break;
            case "DisableRecentMods":
                DisableRecentlyInstalled(gtaPath, changes, TimeSpan.FromDays(7));
                break;
            case "DisableNonEssentialAsi":
                DisableNonEssentialAsi(gtaPath, changes);
                break;
            case "DisableScripts":
                DisableScripts(gtaPath, changes);
                break;
        }

        return new SafeLaunchPlan { Mode = mode, Changes = changes };
    }

    public async Task ApplyAsync(SafeLaunchPlan plan, IProgress<string>? progress = null)
    {
        var restorePoint = new RestorePoint { OperationName = $"Safe Launch: {plan.Mode}" };
        restorePoint.Entries.AddRange(plan.Changes.Select(c => new RestorePointEntry
        {
            RelativePath = Path.GetRelativePath(AppConfig.Instance.GtaPath, c.FilePath),
            WasEnabled = c.WasEnabled,
        }));
        await RestorePointService.Instance.SaveAsync(restorePoint);

        foreach (var change in plan.Changes)
        {
            try
            {
                if (!change.WillBeEnabled && !change.FilePath.EndsWith(".disabled"))
                    File.Move(change.FilePath, change.FilePath + ".disabled");
                progress?.Report($"Disabled: {Path.GetFileName(change.FilePath)}");
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {Path.GetFileName(change.FilePath)} — {ex.Message}");
            }
        }

        ChangeHistoryService.Instance.Record(ChangeHistoryAction.SafeLaunchApplied, $"Safe Launch applied: {plan.Mode}");
    }

    private void DisableNonEssential(string gtaPath, List<SafeLaunchChange> changes, bool keepLspdfr)
    {
        var pluginsDir = Path.Combine(gtaPath, "plugins", "lspdfr");
        if (!Directory.Exists(pluginsDir)) return;

        foreach (var file in Directory.EnumerateFiles(pluginsDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (keepLspdfr && name.Equals("LSPDFR", StringComparison.OrdinalIgnoreCase)) continue;
            changes.Add(new SafeLaunchChange { FilePath = file, WasEnabled = true, WillBeEnabled = false });
        }
    }

    private static void DisableRecentlyInstalled(string gtaPath, List<SafeLaunchChange> changes, TimeSpan age)
    {
        var cutoff = DateTime.UtcNow - age;
        var dirs = new[] { "plugins", "scripts", "mods" };

        foreach (var dir in dirs)
        {
            var full = Path.Combine(gtaPath, dir);
            if (!Directory.Exists(full)) continue;
            foreach (var file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if (info.CreationTimeUtc >= cutoff && !file.EndsWith(".disabled"))
                    changes.Add(new SafeLaunchChange { FilePath = file, WasEnabled = true, WillBeEnabled = false });
            }
        }
    }

    private static void DisableNonEssentialAsi(string gtaPath, List<SafeLaunchChange> changes)
    {
        var essentialAsi = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "ScriptHookVDotNet.asi", "OpenIV.asi", "HeapAdjuster.asi", "PackfileLimitAdjuster.asi" };

        foreach (var file in Directory.EnumerateFiles(gtaPath, "*.asi"))
        {
            if (!essentialAsi.Contains(Path.GetFileName(file)))
                changes.Add(new SafeLaunchChange { FilePath = file, WasEnabled = true, WillBeEnabled = false });
        }
    }

    private static void DisableScripts(string gtaPath, List<SafeLaunchChange> changes)
    {
        var scriptsDir = Path.Combine(gtaPath, "scripts");
        if (!Directory.Exists(scriptsDir)) return;
        foreach (var file in Directory.EnumerateFiles(scriptsDir, "*", SearchOption.AllDirectories))
        {
            if (ScriptExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && !file.EndsWith(".disabled"))
                changes.Add(new SafeLaunchChange { FilePath = file, WasEnabled = true, WillBeEnabled = false });
        }
    }
}
