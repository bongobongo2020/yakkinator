using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class HistoryPage : System.Windows.Controls.Page
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
