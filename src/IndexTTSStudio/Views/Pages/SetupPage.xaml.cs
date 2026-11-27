using System.ComponentModel;
using IndexTTSStudio.ViewModels;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Pages;

public partial class SetupPage : System.Windows.Controls.Page
{
    private readonly SetupViewModel _viewModel;

    public SetupPage(SetupViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        // A Page hosted outside a NavigationWindow ignores NavigateUri; open links in the browser
        AddHandler(System.Windows.Documents.Hyperlink.RequestNavigateEvent,
            new System.Windows.Navigation.RequestNavigateEventHandler((_, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch { /* no default browser */ }
                e.Handled = true;
            }));

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ArrangeEngines();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SetupViewModel.IsComplete) or nameof(SetupViewModel.IsEchoInstalled)
            or nameof(SetupViewModel.IsIndicInstalled) or nameof(SetupViewModel.IsVoiceDesignInstalled)
            or nameof(SetupViewModel.IsMossDesignInstalled) or nameof(SetupViewModel.IsVoxCpmDesignInstalled))
            Dispatcher.BeginInvoke(ArrangeEngines);
    }

    /// <summary>
    /// Engines that still need installing go first and open; installed ones fold down to their
    /// header (name + "Installed") below them, so the install button is never off screen.
    /// </summary>
    private void ArrangeEngines()
    {
        (CardExpander Card, bool Installed)[] engines =
        [
            (IndexTtsCard, _viewModel.IsComplete),
            (EchoCard, _viewModel.IsEchoInstalled),
            (IndicCard, _viewModel.IsIndicInstalled),
            (VoiceDesignCard, _viewModel.IsVoiceDesignInstalled),
            (MossDesignCard, _viewModel.IsMossDesignInstalled),
            (VoxCpmDesignCard, _viewModel.IsVoxCpmDesignInstalled),
        ];

        // Stable: keeps the usual order within each group
        var ordered = engines.Where(x => !x.Installed).Concat(engines.Where(x => x.Installed)).ToList();
        EngineList.Children.Clear();
        foreach (var (card, installed) in ordered)
        {
            card.IsExpanded = !installed;
            EngineList.Children.Add(card);
        }
    }
}
