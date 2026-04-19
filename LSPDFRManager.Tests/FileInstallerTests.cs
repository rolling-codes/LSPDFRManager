using LSPDFRManager.Models;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class FileInstallerTests
{
    [Fact]
    public void Install_StripsSingleWrapperDirectory_WhenInstallingFromFolder()
    {
        var sourceRoot = Directory.CreateTempSubdirectory("lspdfr-source").FullName;
        var wrapped = Path.Combine(sourceRoot, "My Plugin", "plugins", "lspdfr");
        Directory.CreateDirectory(wrapped);
        File.WriteAllText(Path.Combine(wrapped, "TestPlugin.dll"), "stub");

        var targetRoot = Directory.CreateTempSubdirectory("lspdfr-target").FullName;
        var mod = new ModInfo { SourcePath = sourceRoot };

        FileInstaller.Install(mod, targetRoot);

        var expected = Path.Combine(targetRoot, "plugins", "lspdfr", "TestPlugin.dll");
        Assert.True(File.Exists(expected), "Expected wrapped folder prefix to be removed during install.");
    }
}
