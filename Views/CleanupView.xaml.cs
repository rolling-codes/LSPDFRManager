using System.Windows.Controls;
using LSPDFRManager.ViewModels;

namespace LSPDFRManager.Views;

public partial class CleanupView : UserControl
{
    public CleanupView()
    {
        InitializeComponent();
    }

    private void ProceedToConfirm_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CleanupViewModel vm)
            vm.ProceedToConfirm();
    }
}
