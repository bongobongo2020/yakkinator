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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SetupService>();
        services.AddSingleton<PythonBackendService>();
        services.AddSingleton<TTSApiClient>();
        services.AddSingleton<AudioPlayerService>();
        services.AddSingleton<VoiceLibraryService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
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

        var mainWindow = new MainWindow();
        mainWindow.Show();
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
