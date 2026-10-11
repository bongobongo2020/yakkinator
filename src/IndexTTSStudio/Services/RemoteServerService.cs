using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Models;
using IndexTTSStudio.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace IndexTTSStudio.Services;

/// <summary>
/// Token-protected HTTP API the Android app uses to generate speech on this PC. It sits in
/// front of whichever engine is selected, so the Python and Echo-TTS servers stay bound to
/// 127.0.0.1. Plain HTTP: reach it over Tailscale/a VPN or an HTTPS tunnel, not a raw port forward.
/// </summary>
public class RemoteServerService : IDisposable
{
    private const string Prefix = "/remote/v1";
    private const int MaxTextLength = 20_000;
    private const int MaxCachedUploads = 32;
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg" };

    private readonly SettingsService _settingsService;
    private readonly SetupService _setupService;
    private readonly TTSApiClient _apiClient;
    private readonly EchoBackendService _echo;
    private readonly VoiceLibraryService _voiceLibrary;
    private readonly MainWindowViewModel _mainVm;
    private readonly GenerateViewModel _generateVm;
    // One remote job at a time: engine switches must not happen under a running generation
    private readonly SemaphoreSlim _jobGate = new(1, 1);

    private WebApplication? _app;

    public bool IsRunning => _app != null;
    public string? LastError { get; private set; }
    public event Action? StatusChanged;

    public RemoteServerService(SettingsService settingsService, SetupService setupService, TTSApiClient apiClient,
        EchoBackendService echo, VoiceLibraryService voiceLibrary, MainWindowViewModel mainVm,
        GenerateViewModel generateVm)
    {
        _settingsService = settingsService;
        _setupService = setupService;
        _apiClient = apiClient;
        _echo = echo;
        _voiceLibrary = voiceLibrary;
        _mainVm = mainVm;
        _generateVm = generateVm;
    }

    private AppSettings Settings => _settingsService.Settings;

    public static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Starts, stops or restarts the server to match the saved settings.</summary>
    public async Task ApplySettingsAsync()
    {
        await StopAsync();
        if (!Settings.RemoteEnabled) return;

        if (string.IsNullOrWhiteSpace(Settings.RemoteToken))
        {
            Settings.RemoteToken = NewToken();
            _settingsService.Save();
        }

        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k =>
            {
                k.ListenAnyIP(Settings.RemotePort);
                k.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
                // Generations can take many minutes; the response is only written at the end
                k.Limits.MinResponseDataRate = null;
                k.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(35);
            });

            var app = builder.Build();
            app.Use(AuthorizeAsync);
            MapEndpoints(app);
            await app.StartAsync();
            _app = app;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex is IOException
                ? $"Port {Settings.RemotePort} is already in use."
                : ex.Message;
        }
        StatusChanged?.Invoke();
    }

    public async Task StopAsync()
    {
        var app = _app;
        _app = null;
        if (app == null) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await app.StopAsync(cts.Token);
            await app.DisposeAsync();
        }
        catch { /* best effort */ }
        StatusChanged?.Invoke();
    }

    private async Task AuthorizeAsync(HttpContext ctx, Func<Task> next)
    {
        var expected = Encoding.UTF8.GetBytes(Settings.RemoteToken);
        var header = ctx.Request.Headers.Authorization.ToString();
        var given = header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? Encoding.UTF8.GetBytes(header[7..].Trim())
            : [];

        if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(expected, given))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid or missing access token" });
            return;
        }
        await next();
    }

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet($"{Prefix}/info", () => Results.Json(new
        {
            app = "Yakkinator",
            version = typeof(RemoteServerService).Assembly.GetName().Version?.ToString(3),
            engine = EngineId(_mainVm.SelectedEngine),
            busy = _generateVm.IsGenerating || _jobGate.CurrentCount == 0,
            engines = new[]
            {
                new { id = "indextts", name = "IndexTTS-2", installed = _setupService.IsSetupComplete,
                      ready = _mainVm.IsBackendRunning, supportsEmotion = true },
                new { id = "echo", name = "Echo-TTS", installed = _setupService.IsEchoSetupComplete,
                      ready = _echo.IsRunning, supportsEmotion = false },
            },
        }));

        app.MapGet($"{Prefix}/voices", () => Results.Json(new
        {
            voices = _voiceLibrary.GetVoices().Select(v => new
            {
                name = v.Name,
                size = v.FileSize,
                modified = new DateTimeOffset(v.Modified).ToUnixTimeSeconds(),
                emotionMode = v.EmotionMode,
                emotionAlpha = v.EmotionAlpha,
                emotionVector = v.EmotionVector,
            }),
        }));

        app.MapGet($"{Prefix}/voices/{{name}}/audio", (string name) =>
        {
            var voice = FindVoice(name);
            return voice == null
                ? Error(StatusCodes.Status404NotFound, $"No voice named \"{name}\"")
                : Results.File(voice.FilePath, ContentTypeFor(voice.FilePath), Path.GetFileName(voice.FilePath));
        });

        app.MapPost($"{Prefix}/voices", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var name = form["name"].ToString().Trim();
            var file = form.Files["audio"];
            if (name.Length == 0 || file == null)
                return Error(StatusCodes.Status400BadRequest, "Both a name and an audio file are required");

            var path = await StoreUploadAsync(file, "spk");
            await OnUiAsync(() => _voiceLibrary.SaveVoice(name, path));
            return Results.Json(new { name });
        }).DisableAntiforgery();

        app.MapPost($"{Prefix}/generate", GenerateAsync).DisableAntiforgery();
    }

    private async Task<IResult> GenerateAsync(HttpRequest request, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        string Field(string key, string fallback = "") =>
            form.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : fallback;
        float F(string key, float fallback) =>
            float.TryParse(Field(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        int I(string key, int fallback) =>
            int.TryParse(Field(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

        var text = Field("text").Trim();
        if (text.Length == 0) return Error(StatusCodes.Status400BadRequest, "Text is empty");
        if (text.Length > MaxTextLength)
            return Error(StatusCodes.Status400BadRequest, $"Text is longer than {MaxTextLength} characters");

        TtsEngine engine;
        switch (Field("engine", EngineId(_mainVm.SelectedEngine)))
        {
            case "indextts": engine = TtsEngine.IndexTTS; break;
            case "echo": engine = TtsEngine.EchoTTS; break;
            default: return Error(StatusCodes.Status400BadRequest, "Unknown engine");
        }

        // Voice: an uploaded file, or one from this PC's voice library
        string voicePath;
        var voiceFile = form.Files["voice"];
        if (voiceFile != null)
        {
            voicePath = await StoreUploadAsync(voiceFile, "spk", ct);
        }
        else
        {
            var voice = FindVoice(Field("voice_name"));
            if (voice == null) return Error(StatusCodes.Status400BadRequest, "Pick a voice or upload a voice sample");
            voicePath = voice.FilePath;
        }

        var emotionMode = Field("emotion_mode", "none");
        var emotionAlpha = Math.Clamp(F("emotion_alpha", 0.6f), 0f, 1f);
        var emotionVector = Field("emotion_vector").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? Math.Clamp(f, 0f, 1f) : 0f)
            .Concat(Enumerable.Repeat(0f, 8)).Take(8).ToArray();

        string? emotionAudioPath = null;
        if (engine == TtsEngine.IndexTTS && emotionMode == "audio")
        {
            var emoFile = form.Files["emotion_audio"];
            if (emoFile == null) return Error(StatusCodes.Status400BadRequest, "Emotion mode \"audio\" needs an emotion sample");
            emotionAudioPath = await StoreUploadAsync(emoFile, "emo", ct);
        }

        if (!await _jobGate.WaitAsync(TimeSpan.Zero, ct))
            return Error(StatusCodes.Status409Conflict, "The PC is already generating a remote request. Try again shortly.");
        try
        {
            var switchError = await EnsureEngineAsync(engine, ct);
            if (switchError != null) return switchError;

            var result = engine == TtsEngine.EchoTTS
                ? await _echo.GenerateAsync(text, voicePath,
                    new EchoSamplerSettings(
                        Math.Clamp(I("echo_steps", 40), 1, 200),
                        F("echo_cfg_text", 3.0f),
                        F("echo_cfg_speaker", 8.0f),
                        I("echo_seed", 0)),
                    ct: ct)
                : await _apiClient.GenerateAsync(new TTSRequest
                {
                    Text = text,
                    VoiceFilePath = voicePath,
                    EmotionMode = emotionMode,
                    EmotionAudioPath = emotionAudioPath,
                    EmotionAlpha = emotionAlpha,
                    EmotionVector = emotionVector,
                    Temperature = F("temperature", Settings.DefaultTemperature),
                    TopP = F("top_p", Settings.DefaultTopP),
                    TopK = I("top_k", Settings.DefaultTopK),
                    MaxTokens = I("max_tokens", 120),
                }, ct);

            if (!result.Success || result.AudioFilePath == null)
                return Error(StatusCodes.Status500InternalServerError, result.ErrorMessage ?? "Generation failed");

            var saveAs = Field("save_voice_as").Trim();
            if (saveAs.Length > 0)
            {
                try
                {
                    await OnUiAsync(() => _voiceLibrary.SaveVoice(saveAs, voicePath, emotionMode, emotionAlpha, emotionVector));
                }
                catch { /* the audio is what matters; the library copy is a convenience */ }
            }

            var audio = await File.ReadAllBytesAsync(result.AudioFilePath, ct);
            request.HttpContext.Response.Headers["X-Job-Id"] = result.JobId;
            return Results.File(audio, "audio/wav", $"yakkinator_{result.JobId}.wav");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Results.Empty; // phone hung up
        }
        catch (Exception ex)
        {
            return Error(StatusCodes.Status500InternalServerError, ex.Message);
        }
        finally
        {
            _jobGate.Release();
        }
    }

    /// <summary>Switches the PC to <paramref name="engine"/> (only one stays loaded) and waits until it can generate.</summary>
    private async Task<IResult?> EnsureEngineAsync(TtsEngine engine, CancellationToken ct)
    {
        if (engine == TtsEngine.EchoTTS && !_setupService.IsEchoSetupComplete)
            return Error(StatusCodes.Status503ServiceUnavailable, "Echo-TTS isn't installed on the PC.");
        if (engine == TtsEngine.IndexTTS && !_setupService.IsSetupComplete)
            return Error(StatusCodes.Status503ServiceUnavailable, "IndexTTS-2 isn't set up on the PC.");

        if (_mainVm.SelectedEngine != engine)
        {
            var switched = await OnUiAsync(() =>
            {
                // Switching unloads the other engine, which would kill a generation started on the PC
                if (_generateVm.IsGenerating) return false;
                _generateVm.SelectedEngine = engine;
                return true;
            });
            if (!switched)
                return Error(StatusCodes.Status409Conflict, "The PC is busy generating with another engine. Try again shortly.");
        }

        if (engine == TtsEngine.IndexTTS)
        {
            // Model load takes a minute or two after a switch
            var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(6);
            while (!_mainVm.IsBackendRunning)
            {
                if (DateTime.UtcNow > deadline || _mainVm.BackendStatus == "Failed to start")
                    return Error(StatusCodes.Status503ServiceUnavailable, $"IndexTTS-2 failed to start: {_mainVm.BackendStatus}");
                await Task.Delay(1000, ct);
            }
        }
        return null;
    }

    private VoiceProfile? FindVoice(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _voiceLibrary.GetVoices().FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Saves an uploaded sample under a content-hash name, so repeated uploads of the same voice hit the
    /// engines' per-file caches, and converts formats the engines can't read (e.g. phone recordings) to WAV.
    /// </summary>
    private static async Task<string> StoreUploadAsync(IFormFile file, string prefix, CancellationToken ct = default)
    {
        var dir = Path.Combine(PathHelper.AppDataDir, "remote-uploads");
        Directory.CreateDirectory(dir);

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = ms.ToArray();
        var digest = Convert.ToHexString(SHA1.HashData(data))[..16].ToLowerInvariant();

        var ext = Path.GetExtension(file.FileName);
        if (!AudioExtensions.Contains(ext)) ext = ".wav";
        var wavPath = Path.Combine(dir, $"{prefix}_{digest}.wav");
        if (File.Exists(wavPath)) return wavPath;

        if (ext.Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllBytesAsync(wavPath, data, ct);
        }
        else
        {
            // Media Foundation picks its decoder from the extension
            var srcPath = Path.Combine(dir, $"{prefix}_{digest}{ext.ToLowerInvariant()}");
            await File.WriteAllBytesAsync(srcPath, data, ct);
            try
            {
                var tmp = wavPath + ".tmp";
                using (var reader = new MediaFoundationReader(srcPath))
                    WaveFileWriter.CreateWaveFile(tmp, reader);
                File.Move(tmp, wavPath, overwrite: true);
            }
            finally
            {
                try { File.Delete(srcPath); } catch { }
            }
        }

        foreach (var old in new DirectoryInfo(dir).EnumerateFiles().OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxCachedUploads))
        {
            try { old.Delete(); } catch { }
        }
        return wavPath;
    }

    private static Task<T> OnUiAsync<T>(Func<T> action) =>
        System.Windows.Application.Current.Dispatcher.InvokeAsync(action).Task;

    private static string EngineId(TtsEngine engine) => engine == TtsEngine.EchoTTS ? "echo" : "indextts";

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp3" => "audio/mpeg",
        ".flac" => "audio/flac",
        _ => "audio/wav",
    };

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

    /// <summary>IPv4 addresses the phone could use, Tailscale (100.64.0.0/10) first.</summary>
    public static List<string> DetectAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Select(ua => ua.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
            .Where(a => a.GetAddressBytes() is not [169, 254, ..])
            .Distinct()
            .ToList();
        return addresses
            .OrderByDescending(a => a.GetAddressBytes() is [100, >= 64 and <= 127, ..])
            .Select(a => a.ToString())
            .ToList();
    }

    /// <summary>The base URL the phone should use for <paramref name="address"/> (a bare IP/host, or a full tunnel URL).</summary>
    public string BaseUrlFor(string address)
    {
        address = address.Trim().TrimEnd('/');
        return address.Contains("://") ? address : $"http://{address}:{Settings.RemotePort}";
    }

    /// <summary>What the pairing QR code encodes.</summary>
    public string PairingUri(string address) =>
        $"yakkinator://pair?url={Uri.EscapeDataString(BaseUrlFor(address))}&token={Uri.EscapeDataString(Settings.RemoteToken)}";

    public void Dispose()
    {
        // Run off the UI thread: StopAsync may wait on requests that marshal to it
        try { Task.Run(StopAsync).Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* best effort on exit */ }
        GC.SuppressFinalize(this);
    }
}
