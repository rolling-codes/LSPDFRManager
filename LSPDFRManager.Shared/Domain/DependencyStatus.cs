namespace LSPDFRManager.Domain;

public enum DependencyStatus
{
    Installed,
    Missing,
    Disabled,
    Duplicate,
    WrongLocation,
    UnknownVersion,
    Optional,
    Recommended,
    Ignored,
}
