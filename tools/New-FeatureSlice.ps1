param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string]$Name,

    [switch]$WithArchitectureTest
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$featureDir = Join-Path $repoRoot "Features\$Name"
$commandsDir = Join-Path $featureDir 'Commands'
$modelsDir = Join-Path $featureDir 'Models'
$testsDir = Join-Path $repoRoot 'LSPDFRManager.Tests'

New-Item -ItemType Directory -Path $commandsDir -Force | Out-Null
New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
New-Item -ItemType Directory -Path $testsDir -Force | Out-Null

function Write-FileIfMissing {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    if (Test-Path -LiteralPath $Path) {
        Write-Host "Exists: $Path"
        return
    }

    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
    Write-Host "Created: $Path"
}

$interfacePath = Join-Path $featureDir "I$($Name)Controller.cs"
$controllerPath = Join-Path $featureDir "$($Name)WorkflowController.cs"
$modulePath = Join-Path $featureDir "$($Name)FeatureModule.cs"
$requestPath = Join-Path $modelsDir "$($Name)Request.cs"
$resultPath = Join-Path $modelsDir "$($Name)Result.cs"
$commandPath = Join-Path $commandsDir "Run$($Name)Command.cs"
$testPath = Join-Path $testsDir "$($Name)ControllerTests.cs"
$architectureTestPath = Join-Path $testsDir "$($Name)ArchitectureTests.cs"

Write-FileIfMissing $interfacePath @"
using LSPDFRManager.Core.Features;
using LSPDFRManager.Features.$Name.Models;

namespace LSPDFRManager.Features.$Name;

public interface I$($Name)Controller : IFeatureController
{
    Task<$($Name)Result> RunAsync($($Name)Request request, CancellationToken cancellationToken = default);
}
"@

Write-FileIfMissing $controllerPath @"
using LSPDFRManager.Core.Commands;
using LSPDFRManager.Features.$Name.Models;

namespace LSPDFRManager.Features.$Name;

public sealed class $($Name)WorkflowController : I$($Name)Controller
{
    public string FeatureKey => "$Name";
    public IReadOnlyDictionary<string, IAppCommand> Commands { get; } =
        new Dictionary<string, IAppCommand>();

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<$($Name)Result> RunAsync($($Name)Request request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new $($Name)Result(true));
    }
}
"@

Write-FileIfMissing $modulePath @"
using LSPDFRManager.Core.Features;

namespace LSPDFRManager.Features.$Name;

public sealed class $($Name)FeatureModule : IFeatureModule
{
    public $($Name)FeatureModule()
    {
        Controller = new $($Name)WorkflowController();
        ViewModel = Controller;
    }

    public string Key => "$Name";
    public object ViewModel { get; }
    public IFeatureController Controller { get; }
}
"@

Write-FileIfMissing $requestPath @"
namespace LSPDFRManager.Features.$Name.Models;

public sealed record $($Name)Request(string Name);
"@

Write-FileIfMissing $resultPath @"
namespace LSPDFRManager.Features.$Name.Models;

public sealed record $($Name)Result(bool Success);
"@

Write-FileIfMissing $commandPath @"
using LSPDFRManager.Core.Commands;
using LSPDFRManager.Features.$Name.Models;

namespace LSPDFRManager.Features.$Name.Commands;

public sealed class Run$($Name)Command : IAppCommand
{
    private readonly I$($Name)Controller _controller;

    public Run$($Name)Command(I$($Name)Controller controller)
    {
        _controller = controller;
    }

    public string Id => "$Name.Run";

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter = null) => true;

    public Task ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default) =>
        _controller.RunAsync(new $($Name)Request("$Name"), cancellationToken);
}
"@

Write-FileIfMissing $testPath @"
using LSPDFRManager.Features.$Name;
using LSPDFRManager.Features.$Name.Models;
using Xunit;

namespace LSPDFRManager.Tests;

public class $($Name)ControllerTests
{
    [Fact]
    public async Task RunAsync_ReturnsSuccess()
    {
        var controller = new $($Name)WorkflowController();

        var result = await controller.RunAsync(new $($Name)Request("$Name"));

        Assert.True(result.Success);
    }
}
"@

if ($WithArchitectureTest) {
    Write-FileIfMissing $architectureTestPath @"
using Xunit;

namespace LSPDFRManager.Tests;

public class $($Name)ArchitectureTests
{
    [Fact]
    public void $($Name)WorkflowBoundary_IsPreserved()
    {
        // Add a feature-specific boundary assertion when this slice introduces
        // a side effect that should only happen through its controller/command.
        Assert.True(true);
    }
}
"@
}

Write-Host ''
Write-Host 'Next registration step: construct/use this module at the current composition point:'
Write-Host "var $($Name.Substring(0, 1).ToLowerInvariant())$($Name.Substring(1))Module = new $($Name)FeatureModule();"
if ($WithArchitectureTest) {
    Write-Host "Architecture test created: $architectureTestPath"
}
