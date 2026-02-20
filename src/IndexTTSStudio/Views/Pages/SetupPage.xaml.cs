using IndexTTSStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Pages;

public partial class SetupPage : System.Windows.Controls.Page
{
    public SetupPage()
    {
        DataContext = App.Services.GetRequiredService<SetupViewModel>();
        InitializeComponent();
    }
}
