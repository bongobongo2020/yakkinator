namespace IndexTTSStudio.Models;

/// <summary>A language in Indic-Speak's voice library and the speakers trained on it.</summary>
public record IndicLanguage(string Code, string Name, bool IsPreview, IReadOnlyList<string> Speakers)
{
    public string DisplayName => IsPreview ? $"{Name} (preview)" : Name;
}

/// <summary>
/// Indic-Speak's fixed voice library (from the bodhan-ai/indic-speak model card). The model has no
/// voice cloning: a speaker is a name it saw in training. An unknown name doesn't fail, it just
/// gives a blurred, averaged voice, so the UI only offers these.
/// </summary>
public static class IndicVoices
{
    public static IReadOnlyList<IndicLanguage> Languages { get; } =
    [
        new("hi", "Hindi", false, ["Amit", "Kavya"]),
        new("en", "English (Indian)", false,
        [
            "Adarsh", "Akash", "Amit", "Anagha", "Anitha", "Anjali", "Ankur", "Arun", "Aryaman", "Bharati",
            "Chaoba", "Chinmay", "Deepika", "Dhara", "Gwrbw", "Ishfaq", "Ishita", "Itishree", "Kaur", "Kavya",
            "Kiran", "Lakshmi", "Madhukar", "Manpreet", "Moomal", "Parth", "Phulmani", "Prastuti", "Preeti",
            "Rano", "Saba", "Sagar", "Sandeep", "Sansuma", "Sham", "Sibu", "Sourav", "Sravani", "Srijana",
            "Thoibi", "Vaidehi", "Vamsi", "Zaid", "Zoon",
        ]),
        new("bn", "Bengali", false, ["Ishita", "Sourav"]),
        new("mr", "Marathi", false, ["Anagha", "Chinmay"]),
        new("te", "Telugu", false, ["Sravani", "Vamsi"]),
        new("ta", "Tamil", false, ["Anitha", "Arun"]),
        new("gu", "Gujarati", false, ["Dhara", "Parth"]),
        new("kn", "Kannada", false, ["Adarsh", "Deepika"]),
        new("ml", "Malayalam", false, ["Kiran", "Lakshmi"]),
        new("or", "Odia", false, ["Akash", "Itishree"]),
        new("pa", "Punjabi", false, ["Kaur", "Manpreet"]),
        new("as", "Assamese", false, ["Ankur", "Prastuti"]),
        new("ur", "Urdu", false, ["Saba", "Zaid"]),
        new("brx", "Bodo", true, ["Gwrbw", "Sansuma"]),
        new("doi", "Dogri", true, ["Preeti", "Sham"]),
        new("kok", "Konkani", true, ["Anjali", "Sandeep"]),
        new("ks", "Kashmiri", true, ["Ishfaq", "Zoon"]),
        new("mai", "Maithili", true, ["Madhukar", "Vaidehi"]),
        new("ne", "Nepali", true, ["Sagar", "Srijana"]),
        new("mni", "Manipuri", true, ["Chaoba", "Thoibi"]),
        new("sa", "Sanskrit", true, ["Aryaman", "Bharati"]),
        new("sat", "Santali", true, ["Phulmani", "Sibu"]),
        new("sd", "Sindhi", true, ["Moomal", "Rano"]),
        new("bhb", "Bhili", true, ["Bhima", "Dhulji", "Govind", "Jhamku", "Kanku", "Sarju", "Tantya"]),
    ];

    /// <summary>
    /// Suggested styles. Style is free text and still preview quality: the model was trained on
    /// uppercase emotion labels and on short descriptive phrases.
    /// </summary>
    public static IReadOnlyList<string> StyleSuggestions { get; } =
    [
        "",
        "ANGER",
        "FEAR",
        "happiness",
        "sadness",
        "calm, like a teacher explaining",
        "excited news anchor",
    ];

    public static IndicLanguage? Find(string? code) =>
        Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownSpeaker(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        Languages.Any(l => l.Speakers.Contains(name, StringComparer.OrdinalIgnoreCase));
}
