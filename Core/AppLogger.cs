using LSPDFRManager.Services;

namespace LSPDFRManager.Core;

public static class AppLogger
{
    private static readonly string Version = "1.5.0";
    private static readonly string SessionId = Guid.NewGuid().ToString("N")[..8];

    public static event Action<LogEntry>? EntryAdded;
    public static List<LogEntry> Entries { get; } = [];

    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);

    public static void Error(string message, Exception? ex = null) =>
        Log(
            LogLevel.Error,
            ex is null
                ? message
                : $"{message}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    private static void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(level, message, DateTime.Now);

        lock (Entries)
        {
            Entries.Add(entry);
        }

        EntryAdded?.Invoke(entry);

        try
        {
            AppDataPaths.EnsureRootExists();
            File.AppendAllText(
                AppDataPaths.LogFile,
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [v{Version}] [{SessionId}] [{level,7}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

public record LogEntry(LogLevel Level, string Message, DateTime Timestamp);
