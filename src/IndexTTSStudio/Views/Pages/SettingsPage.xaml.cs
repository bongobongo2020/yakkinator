using IndexTTSStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Pages;

public partial class SettingsPage : System.Windows.Controls.Page
{
    public SettingsPage()
    {
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}
