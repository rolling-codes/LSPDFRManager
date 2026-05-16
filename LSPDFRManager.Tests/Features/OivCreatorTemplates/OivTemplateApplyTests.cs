using System.Collections.Generic;
using System.IO;
using System.Linq;
using LSPDFRManager.Domain;
using LSPDFRManager.Features.OivCreatorTemplates;
using LSPDFRManager.Features.OivCreatorTemplates.Models;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests.Features.OivCreatorTemplates;

/// <summary>
/// Phase 2 tests: explicit Apply command behavior, path safety, IsUserEdited, and undo.
/// </summary>
public class OivTemplateApplyTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static OivViewModel CreateVmWithFiles(params string[] sourcePaths)
    {
        var vm = new OivViewModel();
        foreach (var path in sourcePaths)
        {
            vm.CreatorFiles.Add(new OivFileEntry
            {
                SourcePath  = path,
                InstallPath = Path.GetFileName(path),
                Action      = OivFileAction.Add
            });
        }
        return vm;
    }

    // ── Selection has ZERO side effects ───────────────────────────────────────

    [Fact]
    public void Selection_DoesNotChangeDescription()
    {
        var vm = new OivViewModel();
        vm.CreatorDescription = "My custom description";

        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;

        Assert.Equal("My custom description", vm.CreatorDescription);
    }

    [Fact]
    public void Selection_DoesNotChangeVersion()
    {
        var vm = new OivViewModel();
        vm.CreatorVersion = "2.0";

        vm.SelectedTemplateId = OivTemplateId.DlcPack;

        Assert.Equal("2.0", vm.CreatorVersion);
    }

    [Fact]
    public void SelectedTemplate_Property_MapsToSelectedTemplateId()
    {
        var vm = new OivViewModel();
        var template = vm.AvailableTemplates.First(t => t.Id == OivTemplateId.LspdfrPlugin);

        vm.SelectedTemplate = template;

        Assert.Equal(OivTemplateId.LspdfrPlugin, vm.SelectedTemplateId);
    }

    [Fact]
    public void SelectedTemplate_Null_SetsIdToNone()
    {
        var vm = new OivViewModel();
        vm.SelectedTemplate = vm.AvailableTemplates.First();
        vm.SelectedTemplate = null;

        Assert.Equal(OivTemplateId.None, vm.SelectedTemplateId);
    }

    // ── Apply updates only plan metadata keys ────────────────────────────────

    [Fact]
    public void Apply_LspdfrPlugin_UpdatesDescriptionOnly()
    {
        var vm = CreateVmWithFiles(@"C:\mods\test.dll");
        vm.CreatorName        = "TestMod";
        vm.CreatorDescription = "Original description";
        vm.CreatorVersion     = "1.0";
        vm.CreatorAuthor      = "TestAuthor";

        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;
        vm.ApplyTemplateCommand.Execute(null);

        // LSPDFR Plugin plan sets Description only.
        Assert.NotEqual("Original description", vm.CreatorDescription);
        Assert.Contains("LSPDFR", vm.CreatorDescription);
        // Version and Author must be untouched.
        Assert.Equal("1.0", vm.CreatorVersion);
        Assert.Equal("TestAuthor", vm.CreatorAuthor);
    }

    [Fact]
    public void Apply_DlcPack_UpdatesVersionOnly()
    {
        var vm = CreateVmWithFiles(@"C:\mods\dlc.rpf");
        vm.CreatorName        = "MyDlc";
        vm.CreatorDescription = "Original description";
        vm.CreatorVersion     = "1.0";

        vm.SelectedTemplateId = OivTemplateId.DlcPack;
        vm.ApplyTemplateCommand.Execute(null);

        // DLC Pack plan sets Version to "1.0.0".
        Assert.Equal("1.0.0", vm.CreatorVersion);
        // Description must be untouched (DLC pack plan has no Description key).
        Assert.Equal("Original description", vm.CreatorDescription);
    }

    // ── Apply suggests paths for matching files ──────────────────────────────

    [Fact]
    public void Apply_LspdfrPlugin_SuggestsDllPath()
    {
        var vm = CreateVmWithFiles(@"C:\mods\MyCallout.dll", @"C:\mods\config.xml");
        vm.CreatorName = "TestMod";
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;
        vm.ApplyTemplateCommand.Execute(null);

        var dllEntry = vm.CreatorFiles.First(f => f.SourcePath.EndsWith(".dll"));
        Assert.Contains("plugins/LSPDFR/", dllEntry.InstallPath);
        Assert.Contains("MyCallout.dll", dllEntry.InstallPath);

        // XML file should be untouched — no matching rule.
        var xmlEntry = vm.CreatorFiles.First(f => f.SourcePath.EndsWith(".xml"));
        Assert.Equal("config.xml", xmlEntry.InstallPath);
    }

    [Fact]
    public void Apply_DlcPack_SuggestsFilenameMatchForDlcRpf()
    {
        var vm = CreateVmWithFiles(@"C:\mods\dlc.rpf");
        vm.CreatorName = "Police Patrol";
        vm.SelectedTemplateId = OivTemplateId.DlcPack;
        vm.ApplyTemplateCommand.Execute(null);

        var entry = vm.CreatorFiles.First();
        Assert.Contains("dlcpacks/police_patrol/dlc.rpf", entry.InstallPath);
    }

    // ── Apply does NOT overwrite user-edited paths ───────────────────────────

    [Fact]
    public void Apply_DoesNotOverwriteUserEditedInstallPath()
    {
        var vm = CreateVmWithFiles(@"C:\mods\MyPlugin.dll");
        vm.CreatorName = "TestMod";

        // User manually edited the install path.
        vm.CreatorFiles[0].InstallPath = "custom/location/MyPlugin.dll";
        vm.CreatorFiles[0].IsUserEdited = true;

        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;
        vm.ApplyTemplateCommand.Execute(null);

        // Must keep user's custom path.
        Assert.Equal("custom/location/MyPlugin.dll", vm.CreatorFiles[0].InstallPath);
    }

    // ── PathSafety enforcement ───────────────────────────────────────────────

    [Fact]
    public void Apply_ValidRelativePaths_PassPathSafety()
    {
        var vm = CreateVmWithFiles(@"C:\mods\callout.dll");
        vm.CreatorName = "TestMod";
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;
        vm.ApplyTemplateCommand.Execute(null);

        var entry = vm.CreatorFiles.First();
        // The path should be a clean relative path — no traversal.
        Assert.DoesNotContain("..", entry.InstallPath);
        Assert.False(Path.IsPathRooted(entry.InstallPath));
    }

    // ── Undo support ─────────────────────────────────────────────────────────

    [Fact]
    public void Undo_RevertsMetadataChanges()
    {
        var vm = CreateVmWithFiles(@"C:\mods\test.dll");
        vm.CreatorName = "TestMod";
        vm.CreatorDescription = "Original";
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;

        vm.ApplyTemplateCommand.Execute(null);
        Assert.True(vm.CanUndoApply);
        Assert.NotEqual("Original", vm.CreatorDescription);

        vm.UndoApplyTemplateCommand.Execute(null);
        Assert.Equal("Original", vm.CreatorDescription);
        Assert.False(vm.CanUndoApply);
    }

    [Fact]
    public void Undo_RevertsPathChanges()
    {
        var vm = CreateVmWithFiles(@"C:\mods\callout.dll");
        vm.CreatorName = "TestMod";
        var originalPath = vm.CreatorFiles[0].InstallPath;

        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;
        vm.ApplyTemplateCommand.Execute(null);

        Assert.NotEqual(originalPath, vm.CreatorFiles[0].InstallPath);

        vm.UndoApplyTemplateCommand.Execute(null);
        Assert.Equal(originalPath, vm.CreatorFiles[0].InstallPath);
    }

    // ── TemplateApplyStatus feedback ─────────────────────────────────────────

    [Fact]
    public void Apply_SetsTemplateApplyStatus()
    {
        var vm = CreateVmWithFiles(@"C:\mods\test.dll");
        vm.CreatorName = "TestMod";
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;

        vm.ApplyTemplateCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.TemplateApplyStatus));
        Assert.Contains("applied", vm.TemplateApplyStatus);
    }

    [Fact]
    public void Apply_WhenNoTemplateSelected_DoesNothing()
    {
        var vm = CreateVmWithFiles(@"C:\mods\test.dll");
        vm.CreatorName = "TestMod";
        vm.CreatorDescription = "Original";
        vm.SelectedTemplateId = OivTemplateId.None;

        // CanExecute should be false, but calling Execute directly should be a no-op.
        Assert.False(vm.ApplyTemplateCommand.CanExecute(null));
    }

    // ── Phase 3: Suggest-on-add ──────────────────────────────────────────────

    private class FakeFileDialogService : LSPDFRManager.Services.IFileDialogService
    {
        public List<string> FilesToReturn { get; set; } = new();
        public IReadOnlyList<string> PickFiles(string title, string filter, bool multiselect) => FilesToReturn;
    }

    [Fact]
    public void AddCreatorFile_WithActiveTemplate_UsesSuggestedPath()
    {
        var fakeDialog = new FakeFileDialogService();
        fakeDialog.FilesToReturn.Add(@"C:\mods\NewPlugin.dll");
        fakeDialog.FilesToReturn.Add(@"C:\mods\readme.txt");

        var vm = new OivViewModel(fakeDialog);
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;

        vm.AddCreatorFileCommand.Execute(null);

        Assert.Equal(2, vm.CreatorFiles.Count);

        var dllEntry = vm.CreatorFiles.First(f => f.SourcePath.EndsWith(".dll"));
        Assert.Equal("plugins/LSPDFR/NewPlugin.dll", dllEntry.InstallPath);
        Assert.False(dllEntry.IsUserEdited); // It's purely suggested

        var txtEntry = vm.CreatorFiles.First(f => f.SourcePath.EndsWith(".txt"));
        Assert.Equal("readme.txt", txtEntry.InstallPath); // Fallback to filename
    }

    [Fact]
    public void AddCreatorFile_PreviouslyUnsafeName_IsSanitizedAndApplied()
    {
        var fakeDialog = new FakeFileDialogService();
        fakeDialog.FilesToReturn.Add(@"C:\mods\dlc.rpf");

        var vm = new OivViewModel(fakeDialog);
        // "../../Windows" sanitizes to "windows" — no traversal reaches PathSafety.
        // DlcPack rule: dlcpacks/{Sanitize(PackageName)}/dlc.rpf
        vm.CreatorName = "../../Windows";
        vm.SelectedTemplateId = OivTemplateId.DlcPack;

        vm.AddCreatorFileCommand.Execute(null);

        var entry = vm.CreatorFiles.First();
        // Sanitize now strips traversal chars; the result is a safe, applied path.
        Assert.Equal("dlcpacks/windows/dlc.rpf", entry.InstallPath);
    }
}
