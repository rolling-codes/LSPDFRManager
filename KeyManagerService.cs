using LSPDFRManager.Core;
using LSPDFRManager.Models;

namespace LSPDFRManager.Services;

public class KeyManagerService
{
    private static readonly string KeysDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "keys");
    private static readonly string KeysIndexPath = Path.Combine(KeysDir, "keys_index.json");

    public ObservableCollection<ModKey> Keys { get; } = new();
    private static KeyManagerService? _instance;
    public static KeyManagerService Instance => _instance ??= new KeyManagerService();

    private KeyManagerService() => Load();

    // ── Add a key from file ─────────────────────────────
    public ModKey AddKeyFromFile(string filePath, Guid? modId = null, string modName = "")
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException();

        var keyContent = File.ReadAllText(filePath);
        var key = new ModKey
        {
            AssociatedModId = modId ?? Guid.Empty,
            ModName = string.IsNullOrEmpty(modName) ? Path.GetFileNameWithoutExtension(filePath) : modName,
            KeyFileName = Path.GetFileName(filePath),
            KeyContent = keyContent,
            SourcePath = filePath,
            AddedAt = DateTime.Now,
        };

        // Store a copy in the manager's keys folder (optional)
        var storedCopy = Path.Combine(KeysDir, $"{key.Id}_{key.KeyFileName}");
        Directory.CreateDirectory(KeysDir);
        File.WriteAllText(storedCopy, keyContent);
        key.SourcePath = storedCopy;

        Keys.Add(key);
        Save();
        AppLogger.Info($"Key added: {key.KeyFileName} for mod '{key.ModName}'");
        return key;
    }

    // ── Add a key manually (text input) ─────────────────
    public ModKey AddKeyManually(string modName, string fileName, string keyContent, Guid? modId = null)
    {
        var key = new ModKey
        {
            AssociatedModId = modId ?? Guid.Empty,
            ModName = modName,
            KeyFileName = fileName,
            KeyContent = keyContent,
            AddedAt = DateTime.Now,
        };
        var storedCopy = Path.Combine(KeysDir, $"{key.Id}_{key.KeyFileName}");
        Directory.CreateDirectory(KeysDir);
        File.WriteAllText(storedCopy, keyContent);
        key.SourcePath = storedCopy;
        Keys.Add(key);
        Save();
        return key;
    }

    // ── Delete a key ────────────────────────────────────
    public void DeleteKey(Guid id)
    {
        var key = Keys.FirstOrDefault(k => k.Id == id);
        if (key == null) return;
        try { File.Delete(key.SourcePath); } catch { }
        Keys.Remove(key);
        Save();
    }

    // ── Apply key to an installed mod (copy to mod folder)
    public bool ApplyKeyToMod(ModKey key, InstalledMod mod)
    {
        if (string.IsNullOrEmpty(mod.InstallPath)) return false;
        var dest = Path.Combine(mod.InstallPath, key.KeyFileName);
        try
        {
            File.WriteAllText(dest, key.KeyContent);
            AppLogger.Info($"Applied key {key.KeyFileName} to {mod.Name}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to apply key to {mod.Name}", ex);
            return false;
        }
    }

    // ── Auto‑apply all keys for a mod during installation
    public void ApplyAllKeysForMod(InstalledMod mod)
    {
        foreach (var key in Keys.Where(k => k.AssociatedModId == mod.Id))
            ApplyKeyToMod(key, mod);
    }

    // ── Persistence ─────────────────────────────────────
    private void Load()
    {
        if (!File.Exists(KeysIndexPath)) return;
        try
        {
            var json = File.ReadAllText(KeysIndexPath);
            var list = JsonSerializer.Deserialize<List<ModKey>>(json);
            if (list is null) return;
            foreach (var k in list) Keys.Add(k);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Keys load failed: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(KeysDir);
            var json = JsonSerializer.Serialize(Keys.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(KeysIndexPath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Keys save failed: {ex.Message}");
        }
    }
}