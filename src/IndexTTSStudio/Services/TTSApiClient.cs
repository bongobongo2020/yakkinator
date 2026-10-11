using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class TTSApiClient : IDisposable
{
    private static readonly string[] EmotionFieldNames =
        ["emo_joy", "emo_anger", "emo_sadness", "emo_fear", "emo_disgust", "emo_melancholy", "emo_surprise", "emo_calm"];

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly SettingsService _settingsService;

    public TTSApiClient(int port, SettingsService settingsService)
    {
        _baseUrl = $"http://127.0.0.1:{port}";
        // Long texts on slower GPUs (or CPU) can take well over 10 minutes
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        _settingsService = settingsService;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var resp = await _http.GetAsync($"{_baseUrl}/api/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // The server parses numbers with Python float(); "0,6" from a comma-decimal locale would be rejected.
    private static StringContent Num(float value) => new(value.ToString(CultureInfo.InvariantCulture));
    private static StringContent Num(int value) => new(value.ToString(CultureInfo.InvariantCulture));

    private static async Task AddAudioAsync(MultipartFormDataContent form, string name, string path, CancellationToken ct)
    {
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(path, ct));
        content.Headers.ContentType = new MediaTypeHeaderValue(
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                _ => "audio/wav",
            });
        form.Add(content, name, Path.GetFileName(path));
    }

    public async Task<TTSResponse> GenerateAsync(TTSRequest request, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();

        await AddAudioAsync(form, "voice", request.VoiceFilePath, ct);
        form.Add(new StringContent(request.Text), "text");

        // Emotion mode
        form.Add(new StringContent(request.EmotionMode), "emotion_mode");
        form.Add(Num(request.EmotionAlpha), "emotion_alpha");

        if (request.EmotionMode == "audio" && !string.IsNullOrWhiteSpace(request.EmotionAudioPath))
            await AddAudioAsync(form, "emotion_audio", request.EmotionAudioPath, ct);

        if (request.EmotionMode == "vector")
        {
            var v = request.EmotionVector;
            for (var i = 0; i < EmotionFieldNames.Length; i++)
                form.Add(Num(i < v.Length ? v[i] : 0f), EmotionFieldNames[i]);
        }

        // Generation params
        form.Add(Num(request.Temperature), "temperature");
        form.Add(Num(request.TopP), "top_p");
        form.Add(Num(request.TopK), "top_k");
        form.Add(Num(request.MaxTokens), "max_tokens");

        using var response = await _http.PostAsync($"{_baseUrl}/api/tts", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new TTSResponse { Success = false, ErrorMessage = error };
        }

        // Save the returned WAV file
        var jobId = response.Headers.TryGetValues("X-Job-Id", out var ids)
            ? ids.First()
            : OutputHelper.NewJobId();

        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        var outputPath = await OutputHelper.SaveAsync(audioData, jobId, _settingsService.Settings.OutputDirectory, ct);

        return new TTSResponse
        {
            Success = true,
            AudioFilePath = outputPath,
            JobId = jobId
        };
    }

    public void Dispose() => _http.Dispose();
}
