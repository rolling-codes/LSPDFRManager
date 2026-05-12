using System.Xml.Linq;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public class ModConfigService
{
    private static readonly HashSet<string> KnownConfigFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "vehicles.meta",
        "handling.meta",
        "carcols.meta",
        "carvariations.meta",
        "dlclist.xml",
        "extratitleupdatedata.meta",
    };

    private static string BackupDirectory =>
        Path.Combine(AppDataPaths.Root, "mod_config_backups");

    public ModConfigFile LoadFile(string path)
    {
        AppLogger.Info($"[MOD_CONFIG_LOAD] Loading: {path}");
        try
        {
            var content = File.ReadAllText(path);
            var info = new FileInfo(path);
            var (isValid, error) = ValidateXml(content);

            var file = new ModConfigFile
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Content = content,
                IsValid = isValid,
                ValidationError = error,
                LastModified = info.LastWriteTime,
            };

            AppLogger.Info($"[MOD_CONFIG_LOAD] OK: {path} | valid={isValid}");
            return file;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[MOD_CONFIG_ERROR] Failed to load {path}", ex);
            throw;
        }
    }

    public (bool isValid, string? error) ValidateXml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (true, null);

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith('<'))
            return (true, null); // treat as plain text / ini — not XML

        try
        {
            XDocument.Parse(content);
            AppLogger.Info("[MOD_CONFIG_VALIDATE] XML valid");
            return (true, null);
        }
        catch (Exception ex)
        {
            AppLogger.Info($"[MOD_CONFIG_VALIDATE] XML invalid: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public bool SaveFile(ModConfigFile file)
    {
        var (isValid, error) = ValidateXml(file.Content);
        if (!isValid)
        {
            AppLogger.Info($"[MOD_CONFIG_ERROR] Save rejected — invalid XML: {error}");
            return false;
        }

        try
        {
            if (File.Exists(file.FilePath))
            {
                var backupPath = BackupFile(file.FilePath);
                file.BackupPath = backupPath;
            }

            File.WriteAllText(file.FilePath, file.Content);
            file.IsModified = false;
            file.LastModified = DateTime.Now;
            AppLogger.Info($"[MOD_CONFIG_SAVE] Saved: {file.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[MOD_CONFIG_ERROR] Failed to save {file.FilePath}", ex);
            return false;
        }
    }

    public string BackupFile(string path)
    {
        Directory.CreateDirectory(BackupDirectory);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        var backupPath = Path.Combine(BackupDirectory, $"{fileName}_{timestamp}_{uniqueSuffix}{ext}.bak");
        File.Copy(path, backupPath, overwrite: false);
        AppLogger.Info($"[MOD_CONFIG_BACKUP] {path} -> {backupPath}");
        return backupPath;
    }

    public List<ModConfigFile> GetKnownConfigFiles(string modInstallRoot)
    {
        var results = new List<ModConfigFile>();
        if (!Directory.Exists(modInstallRoot))
            return results;

        try
        {
            var files = Directory.EnumerateFiles(modInstallRoot, "*.*", SearchOption.AllDirectories);
            foreach (var filePath in files)
            {
                var name = Path.GetFileName(filePath);
                if (KnownConfigFileNames.Contains(name))
                {
                    try
                    {
                        results.Add(LoadFile(filePath));
                    }
                    catch
                    {
                        // skip unreadable files
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[MOD_CONFIG_ERROR] Scan failed for {modInstallRoot}", ex);
        }

        return results;
    }
}
