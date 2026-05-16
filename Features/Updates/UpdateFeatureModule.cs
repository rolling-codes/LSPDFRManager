using LSPDFRManager.Core.Features;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager.Features.Updates;

public sealed class UpdateFeatureModule : IFeatureModule
{
    public UpdateFeatureModule()
    {
        var controller = new UpdateWorkflowController();
        Controller = controller;
        // SettingsViewModel is the current host for the Updates feature
        ViewModel = new SettingsViewModel(controller);
    }

    public string Key => "Updates";

    public object ViewModel { get; }

    public IFeatureController Controller { get; }
}
