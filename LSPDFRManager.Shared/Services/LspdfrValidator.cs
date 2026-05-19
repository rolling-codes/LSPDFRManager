namespace LSPDFRManager.Services;

public static class LspdfrValidator
{
    public static int CalculateDetectionScore(IEnumerable<string> files)
    {
        int score = 0;

        foreach (var f in files)
        {
            if (f.Contains("plugins/LSPDFR", StringComparison.OrdinalIgnoreCase))
                score += 40;

            if (f.Contains("lspdfr", StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                score += 10;

            if (f.Contains("scripts", StringComparison.OrdinalIgnoreCase))
                score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    public static bool IsValidLspdfrStructure(IEnumerable<string> files)
    {
        return files.Any(f => f.Contains("plugins/LSPDFR", StringComparison.OrdinalIgnoreCase))
            || files.Any(f => f.Contains("lspdfr", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasPluginDll(IEnumerable<string> files)
    {
        return files.Any(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasConfigFile(IEnumerable<string> files)
    {
        return files.Any(f => f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase));
    }
}
