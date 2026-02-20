using IndexTTSStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Pages;

public partial class VoiceLibraryPage : System.Windows.Controls.Page
{
    public VoiceLibraryPage()
    {
        DataContext = App.Services.GetRequiredService<VoiceLibraryViewModel>();
        InitializeComponent();
    }
}
