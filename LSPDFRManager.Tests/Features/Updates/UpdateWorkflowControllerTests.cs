using System;
using System.Threading;
using System.Threading.Tasks;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.Updates;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests.Features.Updates;

public class UpdateWorkflowControllerTests
{
    private class FakeUpdateService : UpdateCheckService
    {
        public UpdateCheckResult ResultToReturn { get; set; } = new();
        public bool ShouldThrow { get; set; }

        public override Task<UpdateCheckResult> CheckAsync()
        {
            if (ShouldThrow) throw new Exception("Network error");
            return Task.FromResult(ResultToReturn);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsUpdateAvailable_WhenServiceSaysYes()
    {
        var fakeService = new FakeUpdateService
        {
            ResultToReturn = new UpdateCheckResult
            {
                UpdateAvailable = true,
                LatestVersion = "4.0.0"
            }
        };
        var controller = new UpdateWorkflowController(fakeService);

        var result = await controller.CheckForUpdatesAsync(CancellationToken.None);

        Assert.True(result.UpdateAvailable);
        Assert.Equal("4.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsError_WhenServiceThrows()
    {
        var fakeService = new FakeUpdateService { ShouldThrow = true };
        var controller = new UpdateWorkflowController(fakeService);

        await Assert.ThrowsAsync<Exception>(() => controller.CheckForUpdatesAsync(CancellationToken.None));
    }
}
