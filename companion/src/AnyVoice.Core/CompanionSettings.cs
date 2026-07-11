namespace AnyVoice.Core;

public sealed record CompanionSettings
{
    public const int CurrentSchemaVersion = 1;

    public static CompanionSettings Default => new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public double Scale { get; init; } = 1.0;

    public double Opacity { get; init; } = 1.0;

    public bool SubtitlesEnabled { get; init; } = true;

    public bool SpeechEnabled { get; init; } = true;

    public bool HotkeyEnabled { get; init; }

    public bool StartWithWindows { get; init; }

    public int SubtitleDurationSeconds { get; init; } = 6;

    public double? WindowLeft { get; init; }

    public double? WindowTop { get; init; }

    public string? FfmpegPath { get; init; }

    public string? WhisperPath { get; init; }

    public string? WhisperModelPath { get; init; }

    public string? AudioDevice { get; init; }

    public bool RetainDiagnosticAudio { get; init; }

    public CompanionSettings Normalize()
    {
        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Scale = ClampFinite(Scale, 0.5, 2.0, 1.0),
            Opacity = ClampFinite(Opacity, 0.35, 1.0, 1.0),
            SubtitleDurationSeconds = Math.Clamp(SubtitleDurationSeconds, 1, 30),
            WindowLeft = NormalizeCoordinate(WindowLeft),
            WindowTop = NormalizeCoordinate(WindowTop),
            FfmpegPath = NormalizeText(FfmpegPath),
            WhisperPath = NormalizeText(WhisperPath),
            WhisperModelPath = NormalizeText(WhisperModelPath),
            AudioDevice = NormalizeText(AudioDevice),
        };
    }

    private static double ClampFinite(
        double value,
        double minimum,
        double maximum,
        double fallback)
    {
        return double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }

    private static double? NormalizeCoordinate(double? value)
    {
        return value is not null && double.IsFinite(value.Value) ? value : null;
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
