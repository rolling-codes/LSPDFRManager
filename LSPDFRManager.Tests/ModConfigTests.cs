using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

public class ModConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"modcfg_{Guid.NewGuid():N}");
    private readonly ModConfigService _service = new();

    public ModConfigTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* best-effort */ }
    }

    // ── 1. LoadFile returns ModConfigFile with correct FileName ───────────

    [Fact]
    public void LoadFile_ReturnsModConfigFile_WithCorrectFileName()
    {
        var path = Path.Combine(_tempDir, "vehicles.meta");
        File.WriteAllText(path, "<vehicles></vehicles>");

        var result = _service.LoadFile(path);

        Assert.Equal("vehicles.meta", result.FileName);
        Assert.Equal(path, result.FilePath);
    }

    // ── 2. ValidateXml returns true for valid XML ─────────────────────────

    [Fact]
    public void ValidateXml_ValidXml_ReturnsTrue()
    {
        var (isValid, error) = _service.ValidateXml("<root><child attr=\"val\">text</child></root>");

        Assert.True(isValid);
        Assert.Null(error);
    }

    // ── 3. ValidateXml returns false + error for malformed XML ────────────

    [Fact]
    public void ValidateXml_MalformedXml_ReturnsFalseWithError()
    {
        var (isValid, error) = _service.ValidateXml("<root><unclosed>");

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.NotEmpty(error);
    }

    // ── 4. ValidateXml returns true for plain text (non-XML) ─────────────

    [Fact]
    public void ValidateXml_PlainText_ReturnsTrueAsValid()
    {
        var (isValid, error) = _service.ValidateXml("SomeKey=SomeValue\nOtherKey=OtherValue");

        Assert.True(isValid);
        Assert.Null(error);
    }

    // ── 5. SaveFile creates a backup before saving ────────────────────────

    [Fact]
    public void SaveFile_ExistingFile_CreatesBackupBeforeSave()
    {
        var path = Path.Combine(_tempDir, "handling.meta");
        File.WriteAllText(path, "<HandlingData></HandlingData>");

        var file = _service.LoadFile(path);
        file.Content = "<HandlingData><Item></Item></HandlingData>";

        var saved = _service.SaveFile(file);

        Assert.True(saved);
        Assert.NotNull(file.BackupPath);
        Assert.True(File.Exists(file.BackupPath));
    }

    // ── 6. Saving with identical content still writes the file correctly ──

    [Fact]
    public void SaveFile_SavedContent_MatchesDiskAfterSecondSave()
    {
        var path = Path.Combine(_tempDir, "carcols.meta");
        File.WriteAllText(path, "<CVehicleModelInfoVarGlobal></CVehicleModelInfoVarGlobal>");

        var file = _service.LoadFile(path);
        file.Content = "<CVehicleModelInfoVarGlobal><Lights/></CVehicleModelInfoVarGlobal>";

        _service.SaveFile(file);

        // Update content and save again — must not leave a stale file on disk.
        file.Content = "<CVehicleModelInfoVarGlobal><Lights/><Item/></CVehicleModelInfoVarGlobal>";
        var saved = _service.SaveFile(file);

        Assert.True(saved);
        var diskContent = File.ReadAllText(path);
        Assert.Equal(file.Content, diskContent);
    }

    // ── 7. SaveCommand is disabled when content is invalid XML ────────────

    [Fact]
    public void ViewModel_SaveCommand_DisabledWhenInvalidXml()
    {
        var vm = new ModConfigViewModel();

        // Simulate a selected file so IsModified can become true
        var path = Path.Combine(_tempDir, "dlclist.xml");
        File.WriteAllText(path, "<SMandatoryPacksData></SMandatoryPacksData>");
        vm.AddFileFromPath(path);

        // Set broken XML
        vm.EditorContent = "<SMandatoryPacksData><unclosed>";

        Assert.True(vm.HasValidationError);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    // ── 8. ValidationError is set when content is malformed XML ──────────

    [Fact]
    public void ViewModel_ValidationError_SetWhenMalformedXml()
    {
        var vm = new ModConfigViewModel();

        var path = Path.Combine(_tempDir, "carvariations.meta");
        File.WriteAllText(path, "<CVehicleModelInfoVariation></CVehicleModelInfoVariation>");
        vm.AddFileFromPath(path);

        vm.EditorContent = "<CVehicleModelInfoVariation><Item unclosed";

        Assert.True(vm.HasValidationError);
        Assert.NotNull(vm.ValidationError);
        Assert.NotEmpty(vm.ValidationError);
    }
}
