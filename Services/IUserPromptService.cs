using System.Windows;

namespace LSPDFRManager.Services;

public interface IUserPromptService
{
    bool TrySelectModArchive(out string fileName);
    MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image);
}

public sealed class UserPromptService : IUserPromptService
{
    public bool TrySelectModArchive(out string fileName)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Mod Archive",
            Filter = "Mod Archives|*.zip;*.rar;*.7z|All Files|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            fileName = dialog.FileName;
            return true;
        }

        fileName = string.Empty;
        return false;
    }

    public MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        MessageBox.Show(message, title, buttons, image);
}
