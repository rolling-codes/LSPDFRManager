using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class RestorePointTests : CommandCenterTestBase
{
    [Fact]
    public async Task SaveAndReload()
    {
        Directory.CreateDirectory(AppDataPaths.RestorePointsDirectory);
        var svc = new RestorePointService();

        var rp = new RestorePoint { OperationName = "Test Op" };
        rp.Entries.Add(new RestorePointEntry { RelativePath = "plugins/test.dll", WasEnabled = true });
        await svc.SaveAsync(rp);

        var svc2 = new RestorePointService();
        svc2.Load();

        Assert.Contains(svc2.Points, p => p.OperationName == "Test Op");
    }

    [Fact]
    public async Task Delete_RemovesFromIndex()
    {
        Directory.CreateDirectory(AppDataPaths.RestorePointsDirectory);
        var svc = new RestorePointService();
        var rp = new RestorePoint { OperationName = "ToDelete" };
        await svc.SaveAsync(rp);

        await svc.DeleteAsync(rp);

        var svc2 = new RestorePointService();
        svc2.Load();
        Assert.DoesNotContain(svc2.Points, p => p.Id == rp.Id);
    }
}
