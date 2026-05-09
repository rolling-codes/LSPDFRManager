using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = false)]

namespace LSPDFRManager.Tests;

/// <summary>
/// xUnit collection fixture: all CommandCenter test classes share this collection
/// so they run sequentially and don't race on AppConfig.Instance / AppDataPaths singletons.
/// </summary>
[CollectionDefinition("CommandCenter", DisableParallelization = true)]
public class CommandCenterCollection { }

/// <summary>
/// Shared setup for Command Center integration tests.
/// Creates isolated temp directories and overrides AppData/AppConfig paths per test.
/// </summary>
[Collection("CommandCenter")]
public abstract class CommandCenterTestBase : IDisposable
{
    protected readonly string TempDir;
    protected readonly string GtaDir;
    protected readonly string AppDataDir;

    protected CommandCenterTestBase()
    {
        TempDir     = Path.Combine(Path.GetTempPath(), $"lsp_cc_{Guid.NewGuid():N}");
        GtaDir      = Path.Combine(TempDir, "GTA5");
        AppDataDir  = Path.Combine(TempDir, "AppData");

        Directory.CreateDirectory(GtaDir);
        Directory.CreateDirectory(AppDataDir);

        AppDataPaths.OverrideRoot(AppDataDir);
        AppConfig.Instance.GtaPath    = GtaDir;
        AppConfig.Instance.BackupPath = Path.Combine(AppDataDir, "Backups");
    }

    public void Dispose()
    {
        AppDataPaths.ClearOverride();
        try { Directory.Delete(TempDir, true); } catch { }
    }
}
