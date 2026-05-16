using System.IO.Compression;
using System.Text;
using LSPDFRManager.Core;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

/// <summary>
/// Assembles a valid .oiv ZIP from a validated <see cref="OivPackagePlan"/>.
/// Writes only to the caller-supplied <paramref name="outputPath"/>.
/// Never reads or writes the GTA V install folder.
/// </summary>
public sealed class OivPackageBuilder : IOivPackageBuilder
{
    private readonly IOivAssemblyXmlWriter _xmlWriter;

    public OivPackageBuilder() : this(new OivAssemblyXmlWriter()) { }
    public OivPackageBuilder(IOivAssemblyXmlWriter xmlWriter) => _xmlWriter = xmlWriter;

    public async Task<OivBuildResult> BuildAsync(OivPackagePlan plan, string outputPath)
    {
        if (!plan.IsValid)
            return new OivBuildResult(false, $"Plan has {plan.Errors.Count} validation error(s).");

        if (string.IsNullOrWhiteSpace(outputPath))
            return new OivBuildResult(false, "Output path is required.");

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var xml = _xmlWriter.Write(plan);

            var filesWritten = await Task.Run(() => WriteZip(plan, outputPath, xml));
            return new OivBuildResult(true, FilesWritten: filesWritten);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[OIV_BUILD] Failed to build OIV package", ex);
            return new OivBuildResult(false, ex.Message);
        }
    }

    private static int WriteZip(OivPackagePlan plan, string outputPath, string assemblyXml)
    {
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        var asmEntry = zip.CreateEntry("assembly.xml", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(asmEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            writer.Write(assemblyXml);

        foreach (var file in plan.Files)
        {
            var entryPath = $"content/{file.InstallPath.TrimStart('/')}";
            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var src = File.OpenRead(file.SourcePath);
            using var dst = entry.Open();
            src.CopyTo(dst);
        }

        return plan.Files.Count;
    }
}
