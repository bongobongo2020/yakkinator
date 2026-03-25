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
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<PythonBackendService>(sp =>
            new PythonBackendService(sp.GetRequiredService<SettingsService>().Settings.ApiPort));
        services.AddSingleton<TTSApiClient>(sp =>
            new TTSApiClient(sp.GetRequiredService<SettingsService>().Settings.ApiPort));
        services.AddSingleton<AudioPlayerService>();
        services.AddSingleton<VoiceLibraryService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<GenerateViewModel>();
        services.AddTransient<VoiceLibraryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Pages (required by WPF-UI NavigationView service provider)
        services.AddTransient<SetupPage>();
        services.AddTransient<GeneratePage>();
        services.AddTransient<VoiceLibraryPage>();
        services.AddTransient<HistoryPage>();
        services.AddTransient<SettingsPage>();

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

    protected override async void OnExit(ExitEventArgs e)
    {
        // Shutdown backend
        var backend = Services.GetService<PythonBackendService>();
        if (backend != null) await backend.StopAsync();

        Services.Dispose();
        base.OnExit(e);
    }
}
