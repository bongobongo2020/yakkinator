using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class SetupPage : System.Windows.Controls.Page
{
    public SetupPage(SetupViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        // A Page hosted outside a NavigationWindow ignores NavigateUri; open links in the browser
        AddHandler(System.Windows.Documents.Hyperlink.RequestNavigateEvent,
            new System.Windows.Navigation.RequestNavigateEventHandler((_, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch { /* no default browser */ }
                e.Handled = true;
            }));
    }
}
