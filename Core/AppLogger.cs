namespace LSPDFRManager.Core;

/// <summary>
/// Static, thread-safe logger that writes to an in-memory list and to
/// <c>%APPDATA%\LSPDFRManager\app.log</c>.
/// Includes version, session ID, and stack traces for production observability.
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LSPDFRManager", "app.log");

    private static readonly string Version = "1.4.0";
    private static readonly string SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>Raised on every thread that calls a log method when a new entry is written.</summary>
    public static event Action<LogEntry>? EntryAdded;

    /// <summary>All log entries accumulated since the application started.</summary>
    public static readonly List<LogEntry> Entries = [];

    /// <inheritdoc cref="Log"/>
    public static void Info(string message) => Log(LogLevel.Info, message);
    /// <inheritdoc cref="Log"/>
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    /// <summary>Logs at <see cref="LogLevel.Error"/>, optionally appending the exception and stack trace.</summary>
    public static void Error(string message, Exception? ex = null) =>
        Log(LogLevel.Error, ex is not null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message);

    private static void Log(LogLevel level, string message)
    {
        var entry = new LogEntry(level, message, DateTime.Now);
        lock (Entries) Entries.Add(entry);
        EntryAdded?.Invoke(entry);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [v{Version}] [{SessionId}] [{level,7}] {message}\n");
        }
        catch { /* don't let logging crash the app */ }
    }
}

/// <summary>Severity level of a <see cref="LogEntry"/>.</summary>
public enum LogLevel { Info, Warning, Error }

/// <summary>Immutable log entry produced by <see cref="AppLogger"/>.</summary>
public record LogEntry(LogLevel Level, string Message, DateTime Timestamp);
