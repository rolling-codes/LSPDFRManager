namespace LSPDFRManager.Domain;

public enum BrowseApiStatus
{
    Offline,
    Starting,
    Online,
    Error,
    MissingExecutable,
    PortConflict,
}
