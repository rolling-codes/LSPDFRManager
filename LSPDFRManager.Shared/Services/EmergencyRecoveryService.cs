using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class EmergencyRecoveryService
{
    private static readonly string[] OptionalPluginFolders = [@"plugins\lspdfr"];
    private static readonly string[] AsiExceptions = ["ScriptHookVDotNet.asi", "OpenIV.asi", "HeapAdjuster.asi", "PackfileLimitAdjuster.asi"];

    public EmergencyRecoveryPlan BuildPlan(string mode)
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var actions = new List<EmergeryRecoveryAction>();

        switch (mode)
        {
            case "DisableAllOptionalPlugins":
                AddPluginDisables(gtaPath, actions, Path.Combine(gtaPath, "plugins", "lspdfr"), ".dll");
                break;
            case "DisableAllAsiExceptRequired":
                foreach (var file in Directory.EnumerateFiles(gtaPath, "*.asi"))
                {
                    if (!AsiExceptions.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                        actions.Add(new EmergeryRecoveryAction { Description = $"Disable {Path.GetFileName(file)}", AffectedPath = file, WillDisable = true });
                }
                break;
            case "DisableScriptsFolder":
                AddPluginDisables(gtaPath, actions, Path.Combine(gtaPath, "scripts"), ".cs", ".vb", ".lua");
                break;
        }

        return new EmergencyRecoveryPlan { Mode = mode, Actions = actions };
    }

    public async Task ApplyAsync(EmergencyRecoveryPlan plan, IProgress<string>? progress = null)
    {
        var restorePoint = new RestorePoint { OperationName = $"Emergency Recovery: {plan.Mode}" };
        await RestorePointService.Instance.SaveAsync(restorePoint);

        foreach (var action in plan.Actions)
        {
            if (!action.WillDisable) continue;
            try
            {
                if (File.Exists(action.AffectedPath) && !action.AffectedPath.EndsWith(".disabled"))
                {
                    File.Move(action.AffectedPath, action.AffectedPath + ".disabled");
                    progress?.Report($"Disabled: {Path.GetFileName(action.AffectedPath)}");
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {Path.GetFileName(action.AffectedPath)} — {ex.Message}");
            }
        }

        ChangeHistoryService.Instance.Record(ChangeHistoryAction.SafeLaunchApplied, $"Emergency Recovery applied: {plan.Mode}");
    }

    private static void AddPluginDisables(string gtaPath, List<EmergeryRecoveryAction> actions, string dir, params string[] extensions)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!extensions.Contains(ext)) continue;
            if (file.EndsWith(".disabled")) continue;
            actions.Add(new EmergeryRecoveryAction { Description = $"Disable {Path.GetFileName(file)}", AffectedPath = file, WillDisable = true });
        }
    }
}
