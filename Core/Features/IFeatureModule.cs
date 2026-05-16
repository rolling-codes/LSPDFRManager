namespace LSPDFRManager.Core.Features;

public interface IFeatureModule
{
    string Key { get; }

    object ViewModel { get; }

    IFeatureController Controller { get; }
}
