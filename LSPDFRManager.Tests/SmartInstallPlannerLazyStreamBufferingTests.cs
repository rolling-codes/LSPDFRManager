using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class SmartInstallPlannerLazyStreamBufferingTests : CommandCenterTestBase
{
    private static readonly MethodInfo OpenArchiveEntryStreamMethod =
        typeof(SmartInstallPlanner).GetMethod("OpenArchiveEntryStream", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SmartInstallPlanner.OpenArchiveEntryStream was not found.");

    private string CreateZipArchive(params (string path, byte[] content)[] entries)
    {
        var zipPath = Path.Combine(TempDir, $"planner_lazy_{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        foreach (var (path, content) in entries)
        {
            var entry = zip.CreateEntry(path);
            using var stream = entry.Open();
            stream.Write(content);
        }

        return zipPath;
    }

    private string CreateTarArchive(params (string path, byte[] content)[] entries)
    {
        var tarPath = Path.Combine(TempDir, $"planner_lazy_{Guid.NewGuid():N}.tar");
        using var file = File.Create(tarPath);
        using var writer = new TarWriter(file, TarEntryFormat.V7, leaveOpen: false);

        foreach (var (path, content) in entries)
        {
            using var data = new MemoryStream(content);
            var entry = new V7TarEntry(TarEntryType.V7RegularFile, path)
            {
                DataStream = data
            };

            writer.WriteEntry(entry);
        }

        return tarPath;
    }

    [Fact]
    public void OpenArchiveEntryStream_TextBuffersAreIndependentAndSeekable()
    {
        var archive = CreateZipArchive(
            ("readme.txt", Bytes("README BUFFER")),
            ("install.txt", Bytes("INSTALL BUFFER")));

        using var readme = OpenBufferedEntry(archive, "readme.txt");
        using var install = OpenBufferedEntry(archive, "install.txt");

        Assert.True(readme.CanSeek);
        Assert.True(install.CanSeek);
        Assert.Equal(0, readme.Position);
        Assert.Equal(0, install.Position);

        Assert.Equal("README", ReadChars(readme, 6));

        Assert.Equal(6, readme.Position);
        Assert.Equal(0, install.Position);
        Assert.Equal("INSTALL BUFFER", ReadAllText(install));
    }

    [Fact]
    public void BuildPlan_TarArchiveWithSequentialEntries_ReadsTextAndBinaryEntries()
    {
        var archive = CreateTarArchive(
            ("readme.txt", Bytes("Sequential readme")),
            ("plugins/lspdfr/MyPlugin.dll", new byte[] { 0, 1, 2, 3, 4, 5 }),
            ("plugins/lspdfr/MyPlugin.ini", Bytes("[Main]")));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        Assert.Equal(3, plan.Entries.Count);
        Assert.Contains(plan.Entries, e => e.ArchivePath == "plugins/lspdfr/MyPlugin.dll");
        Assert.Contains(plan.Entries, e => e.ArchivePath == "plugins/lspdfr/MyPlugin.ini");
        Assert.Equal("Sequential readme", plan.ReadmeContent);
        Assert.DoesNotContain(plan.Warnings, w => w.Contains("Could not read archive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPlan_TextBeforeBinaryWithSharedDependency_PreservesOrderingAndBlockingChecks()
    {
        var archive = CreateZipArchive(
            ("readme.txt", Bytes("Install order notes")),
            ("plugins/lspdfr/UltimateBackup/backup.xml", Bytes("<Unit Type=\"Coroner\" />")),
            ("plugins/lspdfr/LemonUI.SHVDN3.dll", new byte[] { 10, 11, 12, 13 }),
            ("plugins/lspdfr/UltimateBackup.dll", new byte[] { 20, 21, 22, 23 }));

        var plan = new SmartInstallPlanner().BuildPlan(archive);

        var dependencyIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("LemonUI.SHVDN3.dll", StringComparison.OrdinalIgnoreCase));
        var pluginIndex = plan.Entries.FindIndex(e => e.ArchivePath.EndsWith("UltimateBackup.dll", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Install order notes", plan.ReadmeContent);
        Assert.True(dependencyIndex >= 0);
        Assert.True(pluginIndex >= 0);
        Assert.True(dependencyIndex < pluginIndex);
        Assert.Contains("Shared Dependencies", plan.InstallOrder);
        Assert.True(plan.RequiresManualConfirmation);
        Assert.Contains(plan.BlockingIssues, issue => issue.Contains("transport/coroner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OpenArchiveEntryStream_BinaryBufferStartsAtBeginningAndSurvivesArchiveClose()
    {
        var binary = Enumerable.Range(0, 512).Select(i => (byte)(i % 251)).ToArray();
        var archive = CreateZipArchive(("plugins/lspdfr/MyPlugin.dll", binary));

        using var stream = OpenBufferedEntry(archive, "plugins/lspdfr/MyPlugin.dll");

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.Equal(0, stream.Position);
        Assert.Equal(binary.Length, stream.Length);

        var copy = new byte[binary.Length];
        var read = stream.Read(copy, 0, copy.Length);

        Assert.Equal(binary.Length, read);
        Assert.Equal(binary, copy);
        Assert.Equal(binary.Length, stream.Position);

        stream.Position = 0;
        Assert.Equal(binary[0], stream.ReadByte());
    }

    private static Stream OpenBufferedEntry(string archivePath, string relativePath)
    {
        try
        {
            return (Stream)OpenArchiveEntryStreamMethod.Invoke(null, [archivePath, relativePath])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string ReadChars(Stream stream, int count)
    {
        var buffer = new byte[count];
        var read = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
