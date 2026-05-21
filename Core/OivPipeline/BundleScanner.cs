using System.Security.Cryptography;
using LSPDFRManager.OivPipeline.Models;

namespace LSPDFRManager.OivPipeline;

public static class BundleScanner
{
    public static IReadOnlyList<BundleFile> Scan(string bundleRoot)
    {
        if (!Directory.Exists(bundleRoot))
            throw new DirectoryNotFoundException($"Bundle root directory not found: {bundleRoot}");

        var files = new List<BundleFile>();

        foreach (var fullPath in Directory.EnumerateFiles(bundleRoot, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(fullPath);
            var relativePath = Path.GetRelativePath(bundleRoot, fullPath).Replace('\\', '/');
            var extension = info.Extension.ToLowerInvariant();
            var contentHash = ComputeSha256(fullPath);

            files.Add(new BundleFile(relativePath, info.Length, extension, contentHash));
        }

        files.Sort((a, b) => StringComparer.Ordinal.Compare(a.RelativePath, b.RelativePath));

        return files.AsReadOnly();
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
