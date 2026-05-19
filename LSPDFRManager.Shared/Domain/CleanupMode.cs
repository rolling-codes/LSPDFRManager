namespace LSPDFRManager.Domain;

public enum CleanupMode
{
    SafeCoreReset           = 1,
    CoreDataReset           = 2,
    CoreRphReset            = 3,
    ThirdPartyPluginCleanup = 4,
    AdvancedManualReview    = 5,
    FullEcosystemCleanout   = 6,
}
