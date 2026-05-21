using LSPDFRManager.Core.Features;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager.Features.Install;

public sealed class InstallFeatureModule : IFeatureModule
{
    public InstallFeatureModule(IUserPromptService? promptService = null)
    {
        var controller = new InstallWorkflowController();
        Controller = controller;
        ViewModel = new InstallViewModel(promptService, controller);
    }

    public string Key => "Install";

    public object ViewModel { get; }

    public IFeatureController Controller { get; }
}
