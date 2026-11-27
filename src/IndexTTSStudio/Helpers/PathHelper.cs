using System.IO;

namespace IndexTTSStudio.Helpers;

public static class PathHelper
{
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndexTTSStudio");

    public static string BackendDir => Path.Combine(AppDataDir, "backend");
    public static string CheckpointsDir => Path.Combine(BackendDir, "checkpoints");
    public static string VoicesDir => Path.Combine(AppDataDir, "voices");
    public static string OutputsDir => Path.Combine(AppDataDir, "outputs");
    public static string SettingsFile => Path.Combine(AppDataDir, "settings.json");
    public static string SetupStateFile => Path.Combine(AppDataDir, "setup-state.json");
    public static string LogFile => Path.Combine(AppDataDir, "app.log");

    // Echo-TTS (C++ engine): portable runtime in bin/, GGUF + ONNX weights in models/
    public static string EchoDir => Path.Combine(AppDataDir, "echo-tts");
    public static string EchoBinDir => Path.Combine(EchoDir, "bin");
    public static string EchoExe => Path.Combine(EchoBinDir, "echo-tts.exe");
    public static string EchoModelsDir => Path.Combine(EchoDir, "models");
    public static string EchoModelFile => Path.Combine(EchoModelsDir, "echo-dit-q8.gguf");
    public static string EchoDacEncoder => Path.Combine(EchoModelsDir, "onnx", "dac_encoder.onnx");
    public static string EchoDacDecoder => Path.Combine(EchoModelsDir, "onnx", "dac_decoder.onnx");
    /// <summary>WAV copies of MP3/FLAC voices; Echo-TTS only reads WAV.</summary>
    public static string EchoVoiceCacheDir => Path.Combine(EchoDir, "voice-cache");

    // Indic-Speak: its own venv (needs transformers v5) and the gated HF model repo
    public static string IndicDir => Path.Combine(AppDataDir, "indic-speak");
    public static string IndicVenvDir => Path.Combine(IndicDir, ".venv");
    public static string IndicPython => Path.Combine(IndicVenvDir, "Scripts", "python.exe");
    public static string IndicModelDir => Path.Combine(IndicDir, "model");

    // Voice designer (Qwen3-TTS VoiceDesign): its own venv (qwen-tts pins transformers 4.57.3)
    public static string VoiceDesignDir => Path.Combine(AppDataDir, "voice-design");
    public static string VoiceDesignVenvDir => Path.Combine(VoiceDesignDir, ".venv");
    public static string VoiceDesignPython => Path.Combine(VoiceDesignVenvDir, "Scripts", "python.exe");
    public static string VoiceDesignModelDir => Path.Combine(VoiceDesignDir, "model");
    /// <summary>Candidate clips from the designer, before one is saved to the library.</summary>
    public static string VoiceDesignCandidatesDir => Path.Combine(VoiceDesignDir, "candidates");

    // MOSS-VoiceGenerator designer: own venv (transformers 5), model + its audio tokenizer
    public static string MossDesignDir => Path.Combine(AppDataDir, "voice-design-moss");
    public static string MossDesignPython => Path.Combine(MossDesignDir, ".venv", "Scripts", "python.exe");
    public static string MossDesignModelDir => Path.Combine(MossDesignDir, "model");
    public static string MossDesignCodecDir => Path.Combine(MossDesignDir, "audio-tokenizer");

    // VoxCPM2 designer: own venv (voxcpm package)
    public static string VoxCpmDesignDir => Path.Combine(AppDataDir, "voice-design-voxcpm");
    public static string VoxCpmDesignPython => Path.Combine(VoxCpmDesignDir, ".venv", "Scripts", "python.exe");
    public static string VoxCpmDesignModelDir => Path.Combine(VoxCpmDesignDir, "model");

    /// <summary>Path to the api_server.py bundled with the .NET app.</summary>
    public static string ApiServerScript => FindBundledScript("api_server.py");

    /// <summary>Path to the indic_server.py bundled with the .NET app.</summary>
    public static string IndicServerScript => FindBundledScript("indic_server.py");

    /// <summary>Path to the voicedesign_server.py bundled with the .NET app.</summary>
    public static string VoiceDesignServerScript => FindBundledScript("voicedesign_server.py");

    /// <summary>Finds a script next to the exe (published), or at dev time in the repo's python/ folder.</summary>
    private static string FindBundledScript(string fileName)
    {
        var exeDir = AppContext.BaseDirectory;
        var published = Path.Combine(exeDir, "python", fileName);
        if (File.Exists(published)) return published;

        // Dev time: walk up to repo root
        var dir = new DirectoryInfo(exeDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "python", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"{fileName} not found");
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(VoicesDir);
        Directory.CreateDirectory(OutputsDir);
    }
}
