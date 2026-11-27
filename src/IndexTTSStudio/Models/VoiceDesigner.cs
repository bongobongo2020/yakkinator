using System.IO;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Models;

/// <summary>The voice-design models Yakkinator can install; each runs python/voicedesign_server.py from its own venv.</summary>
public enum VoiceDesigner
{
    Qwen,
    Moss,
    VoxCpm,
}

/// <summary>Everything the app needs to know to install, start and describe one designer.</summary>
public record VoiceDesignerInfo(
    VoiceDesigner Id,
    string Name,
    /// <summary>What it's good at, shown in the design panel's model picker.</summary>
    string Tagline,
    /// <summary>--engine for voicedesign_server.py.</summary>
    string EngineArg,
    string ModelName,
    string WorkDir,
    string Python,
    string ModelDir,
    string? CodecDir,
    int PortOffset,
    /// <summary>Whether the model takes a language setting (the others infer it from the text).</summary>
    bool UsesLanguage,
    Func<bool> FilesPresent)
{
    public bool IsInstalled => File.Exists(Python) && FilesPresent();
}

public static class VoiceDesigners
{
    public static VoiceDesignerInfo Qwen { get; } = new(
        VoiceDesigner.Qwen, "Qwen3-TTS",
        "Best all-round. 10 languages. Can't do Irish or Scottish accents.",
        "qwen", "Qwen3-TTS-12Hz-1.7B-VoiceDesign",
        PathHelper.VoiceDesignDir, PathHelper.VoiceDesignPython, PathHelper.VoiceDesignModelDir, null, 0, true,
        () => File.Exists(Path.Combine(PathHelper.VoiceDesignModelDir, "model.safetensors")) &&
              File.Exists(Path.Combine(PathHelper.VoiceDesignModelDir, "speech_tokenizer", "model.safetensors")));

    public static VoiceDesignerInfo Moss { get; } = new(
        VoiceDesigner.Moss, "MOSS-VoiceGenerator",
        "Best for Irish accents, good for Scottish and British. English and Chinese.",
        "moss", "MOSS-VoiceGenerator",
        PathHelper.MossDesignDir, PathHelper.MossDesignPython, PathHelper.MossDesignModelDir, PathHelper.MossDesignCodecDir, 1, false,
        () => File.Exists(Path.Combine(PathHelper.MossDesignModelDir, "model.safetensors")) &&
              File.Exists(Path.Combine(PathHelper.MossDesignCodecDir, "model.safetensors.index.json")));

    public static VoiceDesignerInfo VoxCpm { get; } = new(
        VoiceDesigner.VoxCpm, "VoxCPM2",
        "Best for Scottish accents. 48 kHz audio, 30 languages.",
        "voxcpm", "VoxCPM2",
        PathHelper.VoxCpmDesignDir, PathHelper.VoxCpmDesignPython, PathHelper.VoxCpmDesignModelDir, null, 2, false,
        () => File.Exists(Path.Combine(PathHelper.VoxCpmDesignModelDir, "model.safetensors")) &&
              File.Exists(Path.Combine(PathHelper.VoxCpmDesignModelDir, "audiovae.pth")));

    public static IReadOnlyList<VoiceDesignerInfo> All { get; } = [Qwen, Moss, VoxCpm];

    public static VoiceDesignerInfo Get(VoiceDesigner id) => All.First(d => d.Id == id);
}
