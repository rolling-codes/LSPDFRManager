using LSPDFRManager.Domain;
using Microsoft.Win32;

namespace LSPDFRManager.Services;

public class SetupWizardService
{
    public List<GamePathCandidate> DetectGamePaths()
    {
        var candidates = new List<GamePathCandidate>();

        // Steam via registry
        TryAddFromRegistry(candidates, @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder", "Steam/Rockstar Registry");
        TryAddFromRegistry(candidates, @"SOFTWARE\Rockstar Games\Grand Theft Auto V", "InstallFolder", "Rockstar Registry");

        // Common paths
        var commonPaths = new[]
        {
            (@"C:\Program Files\Rockstar Games\Grand Theft Auto V", "Common Rockstar Path"),
            (@"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V", "Default Steam Path"),
            (@"D:\SteamLibrary\steamapps\common\Grand Theft Auto V", "Steam on D:"),
            (@"E:\SteamLibrary\steamapps\common\Grand Theft Auto V", "Steam on E:"),
            (@"D:\Games\Grand Theft Auto V", "Games on D:"),
            (@"D:\Program Files\Rockstar Games\Grand Theft Auto V", "Rockstar on D:"),
        };

        foreach (var (path, source) in commonPaths)
            TryAdd(candidates, path, source);

        // Epic Games Launcher manifests
        var epicDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (Directory.Exists(epicDir))
        {
            foreach (var manifest in Directory.EnumerateFiles(epicDir, "*.item"))
            {
                try
                {
                    var content = File.ReadAllText(manifest);
                    if (!content.Contains("GrandTheftAutoV", StringComparison.OrdinalIgnoreCase)) continue;
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"""InstallLocation""\s*:\s*""([^""]+)""");
                    if (match.Success)
                        TryAdd(candidates, match.Groups[1].Value.Replace("\\\\", "\\"), "Epic Games Launcher");
                }
                catch { }
            }
        }

        return candidates;
    }

    public string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Path is empty.";
        if (!Directory.Exists(path)) return "Folder does not exist.";
        if (!File.Exists(Path.Combine(path, "GTA5.exe"))) return "GTA5.exe not found in folder.";
        if (path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)) return "Warning: path is inside OneDrive — may cause issues.";
        if (path.StartsWith(@"C:\Program Files", StringComparison.OrdinalIgnoreCase)) return "Warning: path is inside Program Files — may require admin access.";
        return "";
    }

    private static void TryAdd(List<GamePathCandidate> list, string path, string source)
    {
        var error = "";
        var isValid = false;

        if (!Directory.Exists(path))
            error = "Folder not found.";
        else if (!File.Exists(Path.Combine(path, "GTA5.exe")))
            error = "GTA5.exe not found.";
        else
            isValid = true;

        list.Add(new GamePathCandidate { Path = path, Source = source, IsValid = isValid, ValidationError = string.IsNullOrEmpty(error) ? null : error });
    }

    private static void TryAddFromRegistry(List<GamePathCandidate> list, string keyPath, string valueName, string source)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                TryAdd(list, value, source);
        }
        catch { }
    }
}
