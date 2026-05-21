using Xunit;

namespace LSPDFRManager.Tests;

public class ArchitectureGuardTests
{
    [Fact]
    public void ViewModels_DoNotReachPastInstallControllerForInstallStart()
    {
        var repo = FindRepoRoot();
        var viewModelFiles = Directory.GetFiles(Path.Combine(repo, "ViewModels"), "*.cs", SearchOption.AllDirectories);

        foreach (var file in viewModelFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain(".Enqueue(", text);
            Assert.DoesNotContain(".EnqueueAsync(", text);
            Assert.DoesNotContain("FileInstaller.", text);
        }
    }

    [Fact]
    public void InstallViewModel_UsesControllerBoundaryForInstallWorkflow()
    {
        var repo = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(repo, "ViewModels", "InstallViewModel.cs"));

        Assert.Contains("IInstallController", text);
        Assert.DoesNotContain("new ModDetector", text);
        Assert.DoesNotContain("new SmartInstallPlanner", text);
    }

    [Fact]
    public void InstallQueueEnqueue_IsLimitedToInstallInfrastructure()
    {
        var repo = FindRepoRoot();
        var allowedRoots = new[]
        {
            Normalize(Path.Combine(repo, "Core")),
            Normalize(Path.Combine(repo, "Features", "Install")),
            Normalize(Path.Combine(repo, "Services")),
        };

        var files = Directory.GetFiles(repo, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}LSPDFRManager.Tests{Path.DirectorySeparatorChar}"));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (!text.Contains(".Enqueue(") && !text.Contains(".EnqueueAsync("))
                continue;
            if (!text.Contains("InstallQueue") && !text.Contains("QueuedInstall"))
                continue;

            Assert.Contains(allowedRoots, root => Normalize(file).StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LSPDFRManager.csproj")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Normalize(string path) => Path.GetFullPath(path);
}
