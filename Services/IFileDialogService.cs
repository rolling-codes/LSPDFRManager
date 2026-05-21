using System.Collections.Generic;

namespace LSPDFRManager.Services;

public interface IFileDialogService
{
    IReadOnlyList<string> PickFiles(string title, string filter, bool multiselect);
}
