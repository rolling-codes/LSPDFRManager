namespace LSPDFRManager.Services;

public sealed class JsonFileStore<T>
{
    private static readonly object FileLock = new();
    private readonly string _path;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public JsonFileStore(string path) => _path = path;

    public T LoadOrDefault(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!File.Exists(_path))
            return factory();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<T>(json, _serializerOptions) ?? factory();
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"Failed to load '{Path.GetFileName(_path)}': {ex.Message}");
            return factory();
        }
    }

    public void Save(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(value, _serializerOptions);
            var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";

            lock (FileLock)
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(_path))
                    File.Replace(tempPath, _path, null);
                else
                    File.Move(tempPath, _path);
            }
        }
        catch (Exception ex)
        {
            Core.AppLogger.Warning($"Failed to save '{Path.GetFileName(_path)}': {ex.Message}");
        }
    }
}
