using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using IndexTTSStudio.Services;
using IndexTTSStudio.ViewModels;
using IndexTTSStudio.Views.Pages;
using IndexTTSStudio.Views.Windows;

namespace IndexTTSStudio;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    public App()
    {
        // Catch exceptions that happen before the dispatcher loop starts
        // (e.g. XAML resource loading errors in InitializeComponent)
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                System.Windows.MessageBox.Show(
                    e.ExceptionObject?.ToString() ?? "Unknown error",
                    "Fatal Startup Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch { }
        };

        // Errors thrown from commands/event handlers on the UI thread (e.g. an unreadable
        // audio file, a file locked by another program) shouldn't take the whole app down.
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                Helpers.PathHelper.EnsureDirectories();
                System.IO.File.AppendAllText(Helpers.PathHelper.LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED: {e.Exception}{Environment.NewLine}");
            }
            catch { }
            System.Windows.MessageBox.Show(
                e.Exception.Message,
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<PythonBackendService>(sp =>
        {
            var settings = sp.GetRequiredService<SettingsService>().Settings;
            return new PythonBackendService(settings.ApiPort, settings.UseFp16);
        });
        services.AddSingleton<TTSApiClient>(sp =>
            new TTSApiClient(sp.GetRequiredService<SettingsService>().Settings.ApiPort, sp.GetRequiredService<SettingsService>()));
        services.AddSingleton<AudioPlayerService>();
        services.AddSingleton<VoiceLibraryService>();

        // ViewModels — singletons: they subscribe to singleton service events, so transient
        // instances leaked (and kept reacting) on every navigation. This also preserves
        // page state such as the entered text when switching tabs.
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SetupViewModel>();
        services.AddSingleton<GenerateViewModel>();
        services.AddSingleton<VoiceLibraryViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Pages (required by WPF-UI NavigationView service provider)
        services.AddSingleton<SetupPage>();
        services.AddSingleton<GeneratePage>();
        services.AddSingleton<VoiceLibraryPage>();
        services.AddSingleton<HistoryPage>();
        services.AddSingleton<SettingsPage>();

        Services = services.BuildServiceProvider();

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to create main window:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "Fatal Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Must stay synchronous: with `async void`, the process could exit at the first await,
        // orphaning the Python backend (and its GPU memory). Disposing the container disposes
        // PythonBackendService, which shuts the backend down.
        Services?.Dispose();
        base.OnExit(e);
    }
}
