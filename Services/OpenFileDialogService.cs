using System.Collections.Generic;

namespace LSPDFRManager.Services;

public class OpenFileDialogService : IFileDialogService
{
    public IReadOnlyList<string> PickFiles(string title, string filter, bool multiselect)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = multiselect
        };

        if (dialog.ShowDialog() == true)
        {
            return dialog.FileNames;
        }

        return [];
    }
}
