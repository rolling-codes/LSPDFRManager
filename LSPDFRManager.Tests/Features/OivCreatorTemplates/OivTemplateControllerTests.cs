using System.Collections.Generic;
using System.Linq;
using LSPDFRManager.Features.OivCreatorTemplates;
using LSPDFRManager.Features.OivCreatorTemplates.Models;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests.Features.OivCreatorTemplates;

public class OivTemplateControllerTests
{
    [Fact]
    public void BuildPlan_LspdfrPlugin_SuggestsCorrectPaths()
    {
        var controller = new OivTemplateController();
        var snapshot = new OivWizardSnapshot("TestMod", "", "", new List<string>());
        
        var plan = controller.BuildPlan(OivTemplateId.LspdfrPlugin, snapshot);

        Assert.Contains(plan.MetadataUpdates, m => m.Key == "Description");
        Assert.Contains(plan.PathSuggestions, p => p.Match == ".dll" && p.TargetPath.Contains("LSPDFR"));
    }

    [Fact]
    public void BuildPlan_DlcPack_UsesPackageNameInPath()
    {
        var controller = new OivTemplateController();
        var snapshot = new OivWizardSnapshot("Police Patrol", "", "", new List<string>());
        
        var plan = controller.BuildPlan(OivTemplateId.DlcPack, snapshot);

        Assert.Contains(plan.PathSuggestions, p => p.TargetPath.Contains("dlcpacks/police_patrol/"));
    }

    [Fact]
    public void ViewModel_Selection_DoesNotChangeMetadata()
    {
        var vm = new OivViewModel();
        vm.CreatorName = "Original Name";
        
        // Act
        vm.SelectedTemplateId = OivTemplateId.LspdfrPlugin;

        // Assert - Rule: Selection has ZERO side effects
        Assert.Equal("Original Name", vm.CreatorName);
    }
}
