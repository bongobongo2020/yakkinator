using System.Windows;
using IndexTTSStudio.Helpers;
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var workArea = SystemParameters.WorkArea;

        // Shrink window if it exceeds the available work area
        if (Width > workArea.Width) Width = workArea.Width;
        if (Height > workArea.Height) Height = workArea.Height;

        // Center, then clamp to work area
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
        if (Left < workArea.Left) Left = workArea.Left;
        if (Top < workArea.Top) Top = workArea.Top;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Set up page navigation service
            NavigationView.SetServiceProvider(App.Services);

            if (!_setupService.IsSetupComplete)
            {
                NavigationView.Navigate(typeof(SetupPage));
            }
            else
            {
                NavigationView.Navigate(typeof(GeneratePage));
                await StartBackendAsync();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize the application:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Application.Current.Shutdown(1);
        }
    }

    private async Task StartBackendAsync()
    {
        try
        {
            _viewModel.BackendStatus = "Starting...";
            _backend.OnLog += msg => Dispatcher.Invoke(() =>
            {
                _viewModel.BackendStatus = msg.Length > 50 ? msg[..50] + "..." : msg;
            });

            await _backend.StartAsync();

            _viewModel.BackendStatus = "Running";
            _viewModel.IsBackendRunning = true;
        }
        catch (Exception ex)
        {
            _viewModel.BackendStatus = "Failed to start";
            _viewModel.IsBackendRunning = false;

            // Save full log for debugging
            var logPath = System.IO.Path.Combine(PathHelper.AppDataDir, "backend_error.log");
            _backend.SaveLogToFile(logPath);

            // Show a concise error message (full log is in the file)
            var shortMessage = ex.Message.Length > 300 ? ex.Message[..300] + "..." : ex.Message;
            var result = System.Windows.MessageBox.Show(
                $"Failed to start the Python backend.\n\n{shortMessage}\n\n" +
                $"Full log: {logPath}\n\n" +
                "Open Setup page to re-download models?",
                "Backend Startup Error",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Error);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                NavigationView.Navigate(typeof(SetupPage));
            }
            else
            {
                // Show the log file
                try
                {
                    System.Diagnostics.Process.Start("notepad.exe", logPath);
                }
                catch { }
            }
        }
    }
}
