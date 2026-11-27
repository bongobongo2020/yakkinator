using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Models;
using IndexTTSStudio.Services;
using Microsoft.Win32;

namespace IndexTTSStudio.ViewModels;

public partial class VoiceLibraryViewModel : ObservableObject
{
    /// <summary>~12 s of varied, neutral speech: a good reference clip for the cloning engines.</summary>
    public const string DefaultDesignScript =
        "Every morning I walk past the little bakery on the corner, and the smell of fresh bread makes me smile. " +
        "It's a small thing, really, but small things matter. Shall we get started?";

    private const int MaxCandidates = 4;

    private readonly VoiceLibraryService _voiceLibrary;
    private readonly AudioPlayerService _audioPlayer;
    private readonly VoiceDesignService _designer;
    private readonly SetupService _setupService;
    private readonly SettingsService _settingsService;
    private readonly MainWindowViewModel _mainVm;
    private readonly GenerateViewModel _generateVm;
    private CancellationTokenSource? _designCts;

    [ObservableProperty] private List<VoiceProfile> _voices = [];
    [ObservableProperty] private bool _isPlaying;

    // Voice designer
    [ObservableProperty] private bool _isDesignerInstalled;
    [ObservableProperty] private bool _isDesignerLoaded;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesignVoicesCommand))]
    private string _designDescription = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesignVoicesCommand))]
    private string _designScript = DefaultDesignScript;
    [ObservableProperty] private string _designLanguage = "English";
    [ObservableProperty] private int _candidateCount = 3;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DesignVoicesCommand))]
    private bool _isDesigning;
    [ObservableProperty] private string _designStatus = "";
    [ObservableProperty] private bool _hasDesignError;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCandidateCommand))]
    private VoiceDesignCandidate? _selectedCandidate;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCandidateCommand))]
    private string _newVoiceName = "";

    public ObservableCollection<VoiceDesignCandidate> Candidates { get; } = [];
    public IReadOnlyList<string> DesignLanguages => VoiceDesignService.Languages;

    /// <summary>Installed designer models for the picker (Qwen, MOSS, VoxCPM2).</summary>
    [ObservableProperty] private List<VoiceDesignerInfo> _designers = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLanguage), nameof(DesignerTagline))]
    private VoiceDesignerInfo? _selectedDesigner;
    /// <summary>Only Qwen3-TTS takes a language; MOSS and VoxCPM2 infer it from the text.</summary>
    public bool ShowLanguage => SelectedDesigner?.UsesLanguage ?? false;
    public string DesignerTagline => SelectedDesigner?.Tagline ?? "";
    /// <summary>Points to the accent designers on the Setup page while they aren't installed.</summary>
    [ObservableProperty] private string _missingDesignersHint = "";
    public IReadOnlyList<int> CandidateCounts { get; } = [1, 2, 3, 4];

    /// <summary>Starting points; the more specific the description, the closer the result.</summary>
    public IReadOnlyList<string> DescriptionExamples { get; } =
    [
        "A raspy middle-aged man with a warm, unhurried baritone and a calm, matter-of-fact delivery.",
        "A bright, friendly young woman in her twenties, clear American accent, upbeat and conversational.",
        "An elderly British gentleman, soft and slightly gravelly, speaking slowly like a storyteller by the fire.",
        "A confident female news anchor in her forties, crisp diction, neutral accent, steady pace.",
        "A nervous teenage boy, voice slightly cracking, talking quickly and a little breathlessly.",
        "A young woman with a thick Irish accent from Cork, cheerful and lively, with a musical lilt. (MOSS)",
        "A middle-aged man with a strong Scottish accent from Glasgow, gruff but warm, steady pace. (VoxCPM2)",
        "An older woman with a broad Scottish accent from Edinburgh, calm and kind, speaking slowly. (VoxCPM2)",
    ];

    public VoiceLibraryViewModel(VoiceLibraryService voiceLibrary, AudioPlayerService audioPlayer,
        VoiceDesignService designer, SetupService setupService, SettingsService settingsService,
        MainWindowViewModel mainVm, GenerateViewModel generateVm)
    {
        _voiceLibrary = voiceLibrary;
        _audioPlayer = audioPlayer;
        _designer = designer;
        _setupService = setupService;
        _settingsService = settingsService;
        _mainVm = mainVm;
        _generateVm = generateVm;

        var settings = settingsService.Settings;
        if (!string.IsNullOrWhiteSpace(settings.VoiceDesignDescription)) _designDescription = settings.VoiceDesignDescription;
        if (!string.IsNullOrWhiteSpace(settings.VoiceDesignScript)) _designScript = settings.VoiceDesignScript;
        if (VoiceDesignService.Languages.Contains(settings.VoiceDesignLanguage)) _designLanguage = settings.VoiceDesignLanguage;
        RefreshDesigners();

        _audioPlayer.OnPlaybackStopped += () =>
            App.Current.Dispatcher.Invoke(() => IsPlaying = false);
        _voiceLibrary.VoicesChanged += Refresh;
        _setupService.OnVoiceDesignSetupCompleted += () =>
            App.Current.Dispatcher.BeginInvoke(RefreshDesigners);
        _designer.StateChanged += () =>
            App.Current.Dispatcher.BeginInvoke(() => IsDesignerLoaded = _designer.IsReady);
        Refresh();
    }

    private void RefreshDesigners()
    {
        Designers = VoiceDesigners.All.Where(d => _setupService.IsDesignerSetupComplete(d.Id)).ToList();
        IsDesignerInstalled = Designers.Count > 0;
        var wanted = SelectedDesigner?.Id ?? _settingsService.Settings.VoiceDesigner;
        SelectedDesigner = Designers.FirstOrDefault(d => d.Id == wanted) ?? Designers.FirstOrDefault();

        var missing = new List<string>();
        if (!_setupService.IsDesignerSetupComplete(VoiceDesigner.Moss)) missing.Add("MOSS for Irish accents");
        if (!_setupService.IsDesignerSetupComplete(VoiceDesigner.VoxCpm)) missing.Add("VoxCPM2 for Scottish accents");
        MissingDesignersHint = missing.Count == 0 ? "" : $"Install {string.Join(" and ", missing)} from the Setup page.";
    }

    partial void OnSelectedDesignerChanged(VoiceDesignerInfo? value)
    {
        if (value == null || _settingsService.Settings.VoiceDesigner == value.Id) return;
        _settingsService.Settings.VoiceDesigner = value.Id;
        try { _settingsService.Save(); } catch { /* a remembered choice is a convenience */ }
    }

    [RelayCommand]
    private void Refresh()
    {
        Voices = _voiceLibrary.GetVoices();
    }

    [RelayCommand]
    private void AddVoice()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac",
            Title = "Add Voice to Library"
        };
        if (dlg.ShowDialog() == true)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            _voiceLibrary.SaveVoice(name, dlg.FileName);
        }
    }

    [RelayCommand]
    private void DeleteVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        _audioPlayer.ReleaseFile(voice.FilePath);
        _voiceLibrary.DeleteVoice(voice);
    }

    [RelayCommand]
    private void PreviewVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        TogglePlay(voice.FilePath);
    }

    private void TogglePlay(string path)
    {
        // Clicking the clip that's playing stops it; clicking another one switches to it
        if (IsPlaying && _audioPlayer.CurrentFile == path)
        {
            _audioPlayer.Stop();
            IsPlaying = false;
            return;
        }
        try
        {
            _audioPlayer.Play(path);
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            DesignStatus = $"Playback failed: {ex.Message}";
        }
    }

    // ---------- Voice designer ----------

    /// <summary>Picking an example copies it into the description box to edit.</summary>
    [ObservableProperty] private string? _selectedExample;

    partial void OnSelectedExampleChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        // "(MOSS)"/"(VoxCPM2)" marks which designer does that accent best: switch to it when installed
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\s*\((MOSS|VoxCPM2)\)$");
        if (match.Success)
        {
            var id = match.Groups[1].Value == "MOSS" ? VoiceDesigner.Moss : VoiceDesigner.VoxCpm;
            if (Designers.FirstOrDefault(d => d.Id == id) is { } designer) SelectedDesigner = designer;
            value = value[..match.Index];
        }
        DesignDescription = value;
    }

    private bool CanDesign() =>
        !IsDesigning && !string.IsNullOrWhiteSpace(DesignDescription) && !string.IsNullOrWhiteSpace(DesignScript);

    /// <summary>Makes several candidates from the description; each sounds different, so the user picks one.</summary>
    [RelayCommand(CanExecute = nameof(CanDesign))]
    private async Task DesignVoicesAsync()
    {
        if (SelectedDesigner is not { } designerModel)
        {
            HasDesignError = true;
            DesignStatus = "Install a voice designer from the Setup page first.";
            return;
        }
        if (_generateVm.IsGenerating)
        {
            HasDesignError = true;
            DesignStatus = "Wait for the current speech generation to finish first.";
            return;
        }

        var description = DesignDescription.Trim();
        var script = DesignScript.Trim();
        var language = DesignLanguage;
        var settings = _settingsService.Settings;
        settings.VoiceDesignDescription = description;
        settings.VoiceDesignScript = script;
        settings.VoiceDesignLanguage = language;
        _settingsService.Save();

        IsDesigning = true;
        HasDesignError = false;
        _designCts = new CancellationTokenSource();
        var ct = _designCts.Token;
        var count = Math.Clamp(CandidateCount, 1, MaxCandidates);
        try
        {
            if (!_designer.IsRunning)
            {
                // Only one model fits on most GPUs: unload the speech engine (it reloads on the next generation)
                DesignStatus = "Freeing GPU memory...";
                if (_mainVm.UnloadEnginesAsync is { } unload) await unload();
            }

            for (var i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                DesignStatus = $"Designing voice {i + 1} of {count}...";
                var candidate = await _designer.DesignAsync(designerModel, description, script, language,
                    onStatus: s => App.Current.Dispatcher.BeginInvoke(() => DesignStatus = s), ct: ct);
                Candidates.Insert(0, candidate);
                while (Candidates.Count > 12) Candidates.RemoveAt(Candidates.Count - 1);
                if (i == 0)
                {
                    SelectedCandidate = candidate;
                    if (string.IsNullOrWhiteSpace(NewVoiceName)) NewVoiceName = SuggestName(description);
                    TogglePlay(candidate.FilePath);
                }
            }
            DesignStatus = count == 1
                ? "Done. Listen, then save it to the library."
                : $"Done. Listen to the {count} candidates and save the one you like.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DesignStatus = "Cancelled.";
        }
        catch (Exception ex)
        {
            HasDesignError = true;
            DesignStatus = ex.Message;
        }
        finally
        {
            _designCts.Dispose();
            _designCts = null;
            IsDesigning = false;
            IsDesignerLoaded = _designer.IsReady;
        }
    }

    [RelayCommand]
    private void CancelDesign() => _designCts?.Cancel();

    [RelayCommand]
    private void PlayCandidate(VoiceDesignCandidate? candidate)
    {
        if (candidate == null) return;
        SelectedCandidate = candidate;
        TogglePlay(candidate.FilePath);
    }

    private bool CanSaveCandidate() => SelectedCandidate != null && !string.IsNullOrWhiteSpace(NewVoiceName);

    [RelayCommand(CanExecute = nameof(CanSaveCandidate))]
    private void SaveCandidate()
    {
        var c = SelectedCandidate;
        if (c == null) return;
        try
        {
            var name = NewVoiceName.Trim();
            _voiceLibrary.SaveVoice(name, c.FilePath,
                design: new VoiceDesignInfo(c.Description, c.Transcript, c.Language, c.Seed, c.Designer.ModelName));
            HasDesignError = false;
            DesignStatus = $"Saved \"{name}\" to the library. Pick it on the Generate page with Echo-TTS or IndexTTS-2.";
            NewVoiceName = "";
        }
        catch (Exception ex)
        {
            HasDesignError = true;
            DesignStatus = $"Couldn't save the voice: {ex.Message}";
        }
    }

    /// <summary>Unloads the designer to give the GPU back (generating speech also does this).</summary>
    [RelayCommand]
    private async Task UnloadDesignerAsync()
    {
        _designCts?.Cancel();
        await _designer.StopAsync();
        IsDesignerLoaded = false;
        DesignStatus = "Voice designer unloaded.";
    }

    /// <summary>"A raspy middle-aged man with..." becomes "Raspy middle-aged man".</summary>
    private static string SuggestName(string description)
    {
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray()))
            .Where(w => w.Length > 0)
            .SkipWhile(w => w.Equals("a", StringComparison.OrdinalIgnoreCase) || w.Equals("an", StringComparison.OrdinalIgnoreCase)
                            || w.Equals("the", StringComparison.OrdinalIgnoreCase))
            .TakeWhile(w => !w.Equals("with", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();
        if (words.Count == 0) return "Designed voice";
        var name = string.Join(' ', words);
        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
