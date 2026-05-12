using System.Windows.Controls;
using Microsoft.Win32;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager.Views;

public partial class ModConfigView : UserControl
{
    public ModConfigView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ModConfigViewModel oldVm)
            oldVm.BrowseRequested -= OnBrowseRequested;

        if (e.NewValue is ModConfigViewModel newVm)
            newVm.BrowseRequested += OnBrowseRequested;
    }

    private void OnBrowseRequested(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Mod Config File",
            Filter = "Config files (*.meta;*.xml;*.ini)|*.meta;*.xml;*.ini|All files (*.*)|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true) return;

        if (DataContext is ModConfigViewModel vm)
        {
            foreach (var path in dialog.FileNames)
                vm.AddFileFromPath(path);
        }
    }
}
