using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ProfileManager
{
    private static ProfileManager? _instance;
    public static ProfileManager Instance => _instance ??= new();

    private List<ModProfile> _profiles = [];

    public IReadOnlyList<ModProfile> Profiles => _profiles;

    public void Load()
    {
        _profiles = [];
        var dir = AppDataPaths.ProfilesDirectory;
        Directory.CreateDirectory(dir);

        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = System.Text.Json.JsonSerializer.Deserialize<ModProfile>(json);
                if (profile is not null) _profiles.Add(profile);
            }
            catch { }
        }

        if (_profiles.Count == 0)
            SeedDefaults();
    }

    private void SeedDefaults()
    {
        var defaults = new[]
        {
            "Vanilla GTA V", "LSPDFR Only", "Stable Patrol",
            "Heavy Modded Patrol", "Testing New Plugins",
            "Minimal Safe Mode", "Recording/Streaming Mode",
        };

        foreach (var name in defaults)
            _profiles.Add(new ModProfile { Name = name });

        SaveAll();
    }

    public ModProfile Create(string name)
    {
        var profile = new ModProfile { Name = name };
        _profiles.Add(profile);
        SaveProfile(profile);
        return profile;
    }

    public ModProfile Duplicate(ModProfile source)
    {
        var copy = new ModProfile
        {
            Name = $"{source.Name} (Copy)",
            Notes = source.Notes,
            Entries = source.Entries.Select(e => new ProfileEntry { RelativePath = e.RelativePath, Enabled = e.Enabled }).ToList(),
        };
        _profiles.Add(copy);
        SaveProfile(copy);
        return copy;
    }

    public void Rename(ModProfile profile, string newName)
    {
        profile.Name = newName;
        SaveProfile(profile);
    }

    public void Delete(ModProfile profile)
    {
        _profiles.Remove(profile);
        var path = ProfilePath(profile);
        if (File.Exists(path)) File.Delete(path);
    }

    public async Task<SafeLaunchPlan> BuildApplyPreviewAsync(ModProfile profile)
    {
        var gtaPath = AppConfig.Instance.GtaPath;
        var changes = new List<SafeLaunchChange>();

        await Task.Run(() =>
        {
            foreach (var entry in profile.Entries)
            {
                var fullPath = Path.Combine(gtaPath, entry.RelativePath);
                var isCurrentlyEnabled = File.Exists(fullPath) && !fullPath.EndsWith(".disabled");
                if (isCurrentlyEnabled != entry.Enabled)
                {
                    changes.Add(new SafeLaunchChange
                    {
                        FilePath = fullPath,
                        WasEnabled = isCurrentlyEnabled,
                        WillBeEnabled = entry.Enabled,
                    });
                }
            }
        });

        return new SafeLaunchPlan { Mode = profile.Name, Changes = changes };
    }

    public async Task ApplyAsync(ModProfile profile, IProgress<string>? progress = null)
    {
        var plan = await BuildApplyPreviewAsync(profile);

        var restorePoint = new RestorePoint { OperationName = $"Apply profile: {profile.Name}" };
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
                if (change.WillBeEnabled && change.FilePath.EndsWith(".disabled"))
                    File.Move(change.FilePath, change.FilePath[..^".disabled".Length]);
                else if (!change.WillBeEnabled && !change.FilePath.EndsWith(".disabled"))
                    File.Move(change.FilePath, change.FilePath + ".disabled");

                progress?.Report($"{(change.WillBeEnabled ? "Enabled" : "Disabled")}: {Path.GetFileName(change.FilePath)}");
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {Path.GetFileName(change.FilePath)} — {ex.Message}");
            }
        }

        profile.LastUsedAt = DateTime.UtcNow;
        AppConfig.Instance.ActiveProfileId = profile.Id;
        AppConfig.Instance.Save();
        SaveProfile(profile);

        ChangeHistoryService.Instance.Record(ChangeHistoryAction.ProfileApplied, $"Applied profile: {profile.Name}");
    }

    public void Export(ModProfile profile, string outputPath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
    }

    public ModProfile? Import(string inputPath)
    {
        try
        {
            var json = File.ReadAllText(inputPath);
            var profile = System.Text.Json.JsonSerializer.Deserialize<ModProfile>(json);
            if (profile is null) return null;
            profile.Id = Guid.NewGuid().ToString();
            _profiles.Add(profile);
            SaveProfile(profile);
            return profile;
        }
        catch { return null; }
    }

    private void SaveAll()
    {
        foreach (var p in _profiles) SaveProfile(p);
    }

    private void SaveProfile(ModProfile profile)
    {
        var path = ProfilePath(profile);
        var json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string ProfilePath(ModProfile profile) =>
        Path.Combine(AppDataPaths.ProfilesDirectory, $"{profile.Id}.json");
}
