using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class TTSApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly SettingsService _settingsService;

    public TTSApiClient(int port, SettingsService settingsService)
    {
        _baseUrl = $"http://127.0.0.1:{port}";
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _settingsService = settingsService;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{_baseUrl}/api/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<TTSResponse> GenerateAsync(TTSRequest request, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();

        // Voice file
        var voiceBytes = await File.ReadAllBytesAsync(request.VoiceFilePath, ct);
        var voiceContent = new ByteArrayContent(voiceBytes);
        voiceContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(voiceContent, "voice", Path.GetFileName(request.VoiceFilePath));

        // Text
        form.Add(new StringContent(request.Text), "text");

        // Emotion mode
        form.Add(new StringContent(request.EmotionMode), "emotion_mode");
        form.Add(new StringContent(request.EmotionAlpha.ToString()), "emotion_alpha");

        if (request.EmotionMode == "audio" && request.EmotionAudioPath != null)
        {
            var emoBytes = await File.ReadAllBytesAsync(request.EmotionAudioPath, ct);
            var emoContent = new ByteArrayContent(emoBytes);
            emoContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(emoContent, "emotion_audio", Path.GetFileName(request.EmotionAudioPath));
        }

        if (request.EmotionMode == "vector")
        {
            var v = request.EmotionVector;
            form.Add(new StringContent(v[0].ToString()), "emo_joy");
            form.Add(new StringContent(v[1].ToString()), "emo_anger");
            form.Add(new StringContent(v[2].ToString()), "emo_sadness");
            form.Add(new StringContent(v[3].ToString()), "emo_fear");
            form.Add(new StringContent(v[4].ToString()), "emo_disgust");
            form.Add(new StringContent(v[5].ToString()), "emo_melancholy");
            form.Add(new StringContent(v[6].ToString()), "emo_surprise");
            form.Add(new StringContent(v[7].ToString()), "emo_calm");
        }

        // Generation params
        form.Add(new StringContent(request.Temperature.ToString()), "temperature");
        form.Add(new StringContent(request.TopP.ToString()), "top_p");
        form.Add(new StringContent(request.TopK.ToString()), "top_k");
        form.Add(new StringContent(request.MaxTokens.ToString()), "max_tokens");

        var response = await _http.PostAsync($"{_baseUrl}/api/tts", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new TTSResponse { Success = false, ErrorMessage = error };
        }

        // Save the returned WAV file
        var jobId = response.Headers.Contains("X-Job-Id")
            ? response.Headers.GetValues("X-Job-Id").First()
            : Guid.NewGuid().ToString()[..8];

        var outputPath = Path.Combine(PathHelper.OutputsDir, $"{jobId}.wav");
        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outputPath, audioData, ct);

        var customDir = _settingsService.Settings.OutputDirectory;
        if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
        {
            var copyPath = Path.Combine(customDir, $"{jobId}.wav");
            await File.WriteAllBytesAsync(copyPath, audioData, ct);
        }

        return new TTSResponse
        {
            Success = true,
            AudioFilePath = outputPath,
            JobId = jobId
        };
    }

    public void Dispose() => _http.Dispose();
}
