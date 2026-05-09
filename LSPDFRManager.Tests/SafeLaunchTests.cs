using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class SafeLaunchTests : CommandCenterTestBase
{
    [Fact]
    public void BuildPlan_IncludesNonEssentialPlugin()
    {
        var dir = Path.Combine(GtaDir, "plugins", "lspdfr");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SomeMod.dll"), "data");

        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        Assert.Contains(plan.Changes, c => c.FilePath.EndsWith("SomeMod.dll"));
    }

    [Fact]
    public void BuildPlan_EmptyWhenNoPlugins()
    {
        var plan = new SafeLaunchManager().BuildPlan("LspdfrOnly");

        Assert.Empty(plan.Changes);
    }
}
