using System.ComponentModel;
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
        viewModel.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GenerateViewModel.EmotionMode)) return;
        if (sender is not GenerateViewModel vm) return;
        RadioNone.IsChecked   = vm.EmotionMode == "none";
        RadioAudio.IsChecked  = vm.EmotionMode == "audio";
        RadioVector.IsChecked = vm.EmotionMode == "vector";
        RadioText.IsChecked   = vm.EmotionMode == "text";
    }

    private void EmotionModeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && DataContext is GenerateViewModel vm)
        {
            vm.EmotionMode = rb.Tag?.ToString() ?? "none";
        }
    }
}
