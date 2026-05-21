namespace LSPDFRManager.OivPipeline.Models;

public sealed record BundleFile(
    string RelativePath,
    long Size,
    string Extension,
    string ContentHash
);

public enum BundleType
{
    VehicleAddon,
    VehicleReplace,
    Els,
    RphPlugin,
    ShvdnScript,
    SirenPack,
    WeaponAddon
}

public enum ConfidenceSource { Manifest, HardSignature }
