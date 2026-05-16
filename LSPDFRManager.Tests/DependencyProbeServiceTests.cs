using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class DependencyProbeServiceTests : IDisposable
{
    private readonly string _gtaPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly DependencyProbeService _sut = new();

    public DependencyProbeServiceTests() => Directory.CreateDirectory(_gtaPath);

    public void Dispose() => Directory.Delete(_gtaPath, recursive: true);

    private DependencyDetectionResult BuildDeps(params string[] names)
    {
        var warnings = names.Select(n => new DependencyWarning { Name = n, Reason = "required", SourceType = ModType.Unknown }).ToList();
        return new DependencyDetectionResult { Warnings = warnings };
    }

    [Fact]
    public void NoDependencies_ReturnsEmpty()
    {
        var result = _sut.Probe(_gtaPath, new DependencyDetectionResult { Warnings = [] });
        Assert.Empty(result.Probes);
        Assert.False(result.HasMissingRequired);
    }

    [Fact]
    public void InvalidGtaPath_AllProbesAreUnknown()
    {
        var result = _sut.Probe(@"C:\does\not\exist", BuildDeps("Script Hook V", "LSPDFR"));
        Assert.All(result.Probes, p => Assert.Equal(DependencyProbeStatus.Unknown, p.Status));
    }

    [Fact]
    public void ScriptHookV_Present_WhenDllExists()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "ScriptHookV.dll"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("Script Hook V"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
        Assert.Contains("ScriptHookV.dll", probe.Evidence);
    }

    [Fact]
    public void ScriptHookV_Missing_WhenNoFilesExist()
    {
        var result = _sut.Probe(_gtaPath, BuildDeps("Script Hook V"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Missing, probe.Status);
        Assert.True(result.HasMissingRequired);
    }

    [Fact]
    public void ScriptHookVDotNet_Present_WhenAsiExists()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "ScriptHookVDotNet.asi"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("ScriptHookVDotNet"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void ScriptHookVDotNet_SHVDN_Alias_Resolved()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "ScriptHookVDotNet3.dll"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("SHVDN"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void AsiLoader_Present_WhenDinput8Exists()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "dinput8.dll"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("ASI Loader"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void Lspdfr_Present_WhenDllExists()
    {
        var pluginsDir = Path.Combine(_gtaPath, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "LSPDFR.dll"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("LSPDFR"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void Lspdfr_Present_WhenDirExists()
    {
        Directory.CreateDirectory(Path.Combine(_gtaPath, "plugins", "lspdfr"));
        var result = _sut.Probe(_gtaPath, BuildDeps("LSPDFR"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void Lspdfr_Missing_WhenNeitherExists()
    {
        var result = _sut.Probe(_gtaPath, BuildDeps("LSPDFR"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Missing, probe.Status);
    }

    [Fact]
    public void RagePluginHook_Present_WhenExeExists()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "RAGEPluginHook.exe"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("RAGE Plugin Hook"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Present, probe.Status);
    }

    [Fact]
    public void OpenIV_AlwaysNotApplicable()
    {
        var result = _sut.Probe(_gtaPath, BuildDeps("OpenIV"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.NotApplicable, probe.Status);
    }

    [Fact]
    public void Mixed_HasMissingRequired_WhenAtLeastOneMissing()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "RAGEPluginHook.exe"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("RAGE Plugin Hook", "Script Hook V"));
        Assert.True(result.HasMissingRequired);
        Assert.Contains(result.Probes, p => p.Status == DependencyProbeStatus.Present);
        Assert.Contains(result.Probes, p => p.Status == DependencyProbeStatus.Missing);
    }

    [Fact]
    public void UnknownDependency_ReturnsUnknownStatus()
    {
        var result = _sut.Probe(_gtaPath, BuildDeps("SomeUnknownMod"));
        var probe = Assert.Single(result.Probes);
        Assert.Equal(DependencyProbeStatus.Unknown, probe.Status);
    }

    [Fact]
    public void Evidence_PopulatedForPresentProbes()
    {
        File.WriteAllText(Path.Combine(_gtaPath, "ScriptHookV.dll"), "");
        var result = _sut.Probe(_gtaPath, BuildDeps("Script Hook V"));
        var probe = Assert.Single(result.Probes);
        Assert.NotEmpty(probe.Evidence);
    }
}
