using System.IO;

namespace LSPDFRManager.Core;

public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "app.log");

    public static event Action<LogEntry>? EntryAdded;
    public static readonly List<LogEntry> Entries = [];

    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message, Exception? ex = null) =>
        Log(LogLevel.Error, ex is not null ? $"{message}: {ex.Message}" : message);

    private static void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(level, message, DateTime.Now);
        lock (Entries) Entries.Add(entry);
        EntryAdded?.Invoke(entry);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{entry.Timestamp:HH:mm:ss}] [{level,7}] {message}\n");
        }
        catch { /* don't let logging crash the app */ }
    }
}

public enum LogLevel { Info, Warning, Error }

public record LogEntry(LogLevel Level, string Message, DateTime Timestamp);
