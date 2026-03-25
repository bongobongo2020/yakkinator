using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class SetupPage : System.Windows.Controls.Page
{
    public SetupPage(SetupViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
