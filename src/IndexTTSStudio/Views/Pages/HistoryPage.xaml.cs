using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class HistoryPage : System.Windows.Controls.Page
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        // The page is reused across navigations; pick up clips generated since the last visit
        Loaded += (_, _) => viewModel.RefreshCommand.Execute(null);
    }
}
