using System.Windows;
using System.Windows.Controls;
using IndexTTSStudio.ViewModels;

namespace IndexTTSStudio.Views.Pages;

public partial class GeneratePage : System.Windows.Controls.Page
{
    public GeneratePage(GenerateViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void EmotionModeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && DataContext is GenerateViewModel vm)
        {
            vm.EmotionMode = rb.Tag?.ToString() ?? "none";
        }
    }
}
