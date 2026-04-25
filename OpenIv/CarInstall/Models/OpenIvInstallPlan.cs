namespace LSPDFRManager.OpenIv.CarInstall.Models;

public class OpenIvInstallPlan
{
    public required CarInstallType Type { get; init; }
    public required string TargetDlcName { get; init; }

    public List<FileOperation> Operations { get; init; } = [];
    public List<XmlPatch> XmlPatches { get; init; } = [];

    public bool RequiresDlcListPatch => XmlPatches.Any();
}
