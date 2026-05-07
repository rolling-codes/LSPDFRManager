using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager.Views;

public partial class InstallView : UserControl
{
    public InstallView() => InitializeComponent();

    private InstallViewModel? VM => DataContext as InstallViewModel;

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
        if (sender is Border b) b.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b) b.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81));
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border b) b.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81));

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0 || VM is null) return;

        if (files.Length == 1)
            _ = VM.DetectAsync(files[0]);
        else
            _ = VM.DetectBatchAsync(files);
    }
}
