namespace AnyVoice.Core.Voice;

public sealed record VoiceStatus(
    VoiceToolPaths Tools,
    IReadOnlyList<string> Microphones,
    IReadOnlyList<string> Errors)
{
    public bool IsReady => Errors.Count == 0;

    public static VoiceStatus From(
        VoiceToolPaths tools,
        IReadOnlyList<string> microphones)
    {
        var errors = new List<string>();
        if (tools.FfmpegPath is null)
        {
            errors.Add("FFmpeg not found.");
        }

        if (tools.WhisperPath is null)
        {
            errors.Add("Whisper not found.");
        }

        if (tools.ModelPath is null)
        {
            errors.Add("Cached Whisper model not found.");
        }

        if (microphones.Count == 0)
        {
            errors.Add("Microphone not found.");
        }

        return new VoiceStatus(tools, microphones, errors);
    }
}
