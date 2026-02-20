using System.Windows;
using IndexTTSStudio.Services;
using IndexTTSStudio.ViewModels;
using IndexTTSStudio.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace IndexTTSStudio.Views.Windows;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _viewModel;
    private readonly PythonBackendService _backend;
    private readonly SetupService _setupService;

    public MainWindow()
    {
        _viewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _backend = App.Services.GetRequiredService<PythonBackendService>();
        _setupService = App.Services.GetRequiredService<SetupService>();
        DataContext = _viewModel;

        InitializeComponent();

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set up page navigation service
        NavigationView.SetServiceProvider(App.Services);

        if (!_setupService.IsSetupComplete)
        {
            // Navigate to setup page on first run
            NavigationView.Navigate(typeof(SetupPage));
        }
        else
        {
            // Auto-start backend
            NavigationView.Navigate(typeof(GeneratePage));
            await StartBackendAsync();
        }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            _viewModel.BackendStatus = "Starting...";
            _backend.OnLog += msg => Dispatcher.Invoke(() =>
                _viewModel.BackendStatus = msg.Length > 50 ? msg[..50] + "..." : msg);

            await _backend.StartAsync();

            _viewModel.BackendStatus = "Running";
            _viewModel.IsBackendRunning = true;
        }
        catch (Exception ex)
        {
            _viewModel.BackendStatus = $"Error: {ex.Message}";
            _viewModel.IsBackendRunning = false;
        }
    }
}
