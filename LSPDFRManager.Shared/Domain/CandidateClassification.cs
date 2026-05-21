namespace LSPDFRManager.Domain;

public enum CandidateClassification
{
    LspdfrCore,
    LspdfrData,
    RphCore,
    ThirdPartyPlugin,
    PluginConfig,
    PluginDataFolder,
    SharedDependency,
    OptionalInfrastructure,
    ManualReview,
    Blocked,
}
