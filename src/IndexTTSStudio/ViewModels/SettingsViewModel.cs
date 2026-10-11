using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Services;
using Microsoft.Win32;
using QRCoder;

namespace IndexTTSStudio.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly RemoteServerService _remote;

    [ObservableProperty] private bool _useFp16;
    [ObservableProperty] private int _apiPort;
    [ObservableProperty] private float _defaultTemperature;
    [ObservableProperty] private float _defaultTopP;
    [ObservableProperty] private int _defaultTopK;
    [ObservableProperty] private string _outputDirectory = "";

    // Remote access (Android app)
    [ObservableProperty] private bool _remoteEnabled;
    [ObservableProperty] private int _remotePort;
    [ObservableProperty] private string _remoteToken = "";
    [ObservableProperty] private List<string> _remoteAddresses = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoteUrl))]
    private string _remoteAddress = "";
    [ObservableProperty] private string _remoteStatus = "Off";
    [ObservableProperty] private BitmapImage? _pairingQr;

    public string RemoteUrl => string.IsNullOrWhiteSpace(RemoteAddress) ? "" : _remote.BaseUrlFor(RemoteAddress);

    public SettingsViewModel(SettingsService settingsService, RemoteServerService remote)
    {
        _settingsService = settingsService;
        _remote = remote;
        var s = settingsService.Settings;
        UseFp16 = s.UseFp16;
        ApiPort = s.ApiPort;
        DefaultTemperature = s.DefaultTemperature;
        DefaultTopP = s.DefaultTopP;
        DefaultTopK = s.DefaultTopK;
        OutputDirectory = s.OutputDirectory;

        _remoteEnabled = s.RemoteEnabled;
        _remotePort = s.RemotePort;
        _remoteToken = s.RemoteToken;
        RefreshAddresses();
        _remoteAddress = !string.IsNullOrWhiteSpace(s.RemoteAddress) ? s.RemoteAddress : RemoteAddresses.FirstOrDefault() ?? "";

        _remote.StatusChanged += () => App.Current.Dispatcher.BeginInvoke(UpdateRemoteStatus);
        UpdateRemoteStatus();
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select Output Directory"
        };
        if (dlg.ShowDialog() == true)
            OutputDirectory = dlg.FolderName;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = _settingsService.Settings;
        s.UseFp16 = UseFp16;
        s.ApiPort = ApiPort;
        s.DefaultTemperature = DefaultTemperature;
        s.DefaultTopP = DefaultTopP;
        s.DefaultTopK = DefaultTopK;
        s.OutputDirectory = OutputDirectory;

        var portChanged = s.RemotePort != RemotePort;
        s.RemotePort = RemotePort;
        s.RemoteAddress = RemoteAddress.Trim();
        _settingsService.Save();

        if (portChanged && s.RemoteEnabled)
            await _remote.ApplySettingsAsync();
        UpdateRemoteStatus();
    }

    async partial void OnRemoteEnabledChanged(bool value)
    {
        _settingsService.Settings.RemoteEnabled = value;
        _settingsService.Settings.RemotePort = RemotePort;
        _settingsService.Save();
        await _remote.ApplySettingsAsync();
        RemoteToken = _settingsService.Settings.RemoteToken; // generated on first enable
        UpdateRemoteStatus();
    }

    partial void OnRemoteAddressChanged(string value) => UpdatePairingQr();

    [RelayCommand]
    private void RegenerateToken()
    {
        // The server reads the token per request, so old phones are locked out immediately
        _settingsService.Settings.RemoteToken = RemoteServerService.NewToken();
        _settingsService.Save();
        RemoteToken = _settingsService.Settings.RemoteToken;
        UpdatePairingQr();
    }

    [RelayCommand]
    private void CopyToken()
    {
        if (RemoteToken.Length > 0) System.Windows.Clipboard.SetText(RemoteToken);
    }

    [RelayCommand]
    private void CopyPairingLink()
    {
        if (RemoteToken.Length > 0 && RemoteAddress.Length > 0)
            System.Windows.Clipboard.SetText(_remote.PairingUri(RemoteAddress));
    }

    [RelayCommand]
    private void RefreshAddresses()
    {
        RemoteAddresses = RemoteServerService.DetectAddresses();
    }

    private void UpdateRemoteStatus()
    {
        RemoteStatus = _remote.IsRunning
            ? $"Listening on port {_settingsService.Settings.RemotePort}"
            : _remote.LastError != null ? $"Failed: {_remote.LastError}" : "Off";
        UpdatePairingQr();
    }

    private void UpdatePairingQr()
    {
        if (!_remote.IsRunning || RemoteToken.Length == 0 || string.IsNullOrWhiteSpace(RemoteAddress))
        {
            PairingQr = null;
            return;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(_remote.PairingUri(RemoteAddress), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(6);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();
        PairingQr = image;
    }
}
