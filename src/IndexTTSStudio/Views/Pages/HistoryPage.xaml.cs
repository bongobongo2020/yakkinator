using IndexTTSStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Pages;

public partial class HistoryPage : System.Windows.Controls.Page
{
    public HistoryPage()
    {
        DataContext = App.Services.GetRequiredService<HistoryViewModel>();
        InitializeComponent();
    }
}
