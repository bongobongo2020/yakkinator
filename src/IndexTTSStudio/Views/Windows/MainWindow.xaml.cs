using System.Windows;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Models;
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
    private readonly EchoBackendService _echo;
    private CancellationTokenSource? _startCts;

    public MainWindow()
    {
        _viewModel = App.Services.GetRequiredService<MainWindowViewModel>();
        _backend = App.Services.GetRequiredService<PythonBackendService>();
        _setupService = App.Services.GetRequiredService<SetupService>();
        _echo = App.Services.GetRequiredService<EchoBackendService>();
        _viewModel.SelectedEngine = App.Services.GetRequiredService<SettingsService>().Settings.Engine;
        DataContext = _viewModel;

        InitializeComponent();

        // BeginInvoke (not Invoke): the backend's output reader thread shouldn't block on
        // the UI thread for every log line during verbose inference.
        _backend.OnLog += msg => Dispatcher.BeginInvoke(() =>
        {
            _viewModel.BackendStatus = msg.Length > 50 ? msg[..50] + "..." : msg;
        });
        _setupService.OnSetupCompleted += () => Dispatcher.BeginInvoke(async () =>
        {
            if (_viewModel.SelectedEngine == TtsEngine.IndexTTS && !_backend.IsRunning)
                await StartBackendAsync();
        });
        _viewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedEngine))
                await ApplyEngineAsync(_viewModel.SelectedEngine);
        };

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

            // Remote access for the Android app (no-op unless enabled in Settings)
            await App.Services.GetRequiredService<RemoteServerService>().ApplySettingsAsync();

            if (_viewModel.SelectedEngine == TtsEngine.EchoTTS)
            {
                // Echo-TTS loads lazily on the first generation (it needs the chosen voice)
                NavigationView.Navigate(_setupService.IsEchoSetupComplete ? typeof(GeneratePage) : typeof(SetupPage));
            }
            else if (!_setupService.IsSetupComplete)
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

    /// <summary>Only one engine stays loaded, so switching frees the other one's GPU memory.</summary>
    private async Task ApplyEngineAsync(TtsEngine engine)
    {
        try
        {
            if (engine == TtsEngine.EchoTTS)
            {
                _startCts?.Cancel();
                await _backend.StopAsync();
                _viewModel.IsBackendRunning = false;
                _viewModel.BackendStatus = "Stopped";
            }
            else
            {
                await _echo.StopAsync();
                if (_setupService.IsSetupComplete)
                    await StartBackendAsync();
                else
                    _viewModel.BackendStatus = "IndexTTS-2 isn't set up yet. Run it from the Setup page.";
            }
        }
        catch (Exception ex)
        {
            _viewModel.BackendStatus = $"Switching engine failed: {ex.Message}";
        }
    }

    private bool _startingBackend;

    private async Task StartBackendAsync()
    {
        if (_startingBackend) return;
        _startingBackend = true;
        _startCts = new CancellationTokenSource();
        var ct = _startCts.Token;
        var cancelled = false;
        try
        {
            _viewModel.BackendStatus = "Starting...";

            await _backend.StartAsync(ct);

            // Queued after any pending log-line updates, so a stale log line can't overwrite it
            await Dispatcher.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return; // switched to Echo-TTS meanwhile
                _viewModel.BackendStatus = "Running";
                _viewModel.IsBackendRunning = true;
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Engine switched away while IndexTTS was loading
            cancelled = true;
            _viewModel.BackendStatus = "Stopped";
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
        finally
        {
            _startingBackend = false;
            _startCts.Dispose();
            _startCts = null;
        }

        // Switched away and back again before the cancelled start finished unwinding
        if (cancelled && _viewModel.SelectedEngine == TtsEngine.IndexTTS && !_backend.IsRunning)
            await StartBackendAsync();
    }
}
