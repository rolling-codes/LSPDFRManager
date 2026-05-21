using LSPDFRManager.LocalApi;

namespace LSPDFRManager.ViewModels;

public class ReactPreviewViewModel : ObservableObject
{
    private string _statusText = "Starting local API…";
    private bool _isReady;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsReady
    {
        get => _isReady;
        set => SetProperty(ref _isReady, value);
    }

    public string Uri => LocalApiHost.BaseUrl + "/";

    public async Task WaitForReadyAsync()
    {
        try
        {
            await LocalApiHost.PortTask;
            IsReady = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Local API failed to start: {ex.Message}";
        }
    }
}
