namespace LSPDFRManager.Services;

public static class LogFileReader
{
    /// <summary>
    /// Reads all lines from a log file using FileShare.ReadWrite so the read
    /// succeeds even when RPH or another process is actively writing the file.
    /// </summary>
    public static string[] ReadAllLines(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            lines.Add(line);
        return lines.ToArray();
    }
}
