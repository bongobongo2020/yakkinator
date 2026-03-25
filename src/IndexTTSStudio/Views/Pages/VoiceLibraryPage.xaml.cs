using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class VoiceLibraryPage : System.Windows.Controls.Page
{
    public VoiceLibraryPage(VoiceLibraryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
