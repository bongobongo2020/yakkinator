using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class SettingsPage : System.Windows.Controls.Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
