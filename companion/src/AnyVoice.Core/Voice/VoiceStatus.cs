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
            errors.Add("未找到 FFmpeg。");
        }

        if (tools.WhisperPath is null)
        {
            errors.Add("未找到 Whisper。");
        }

        if (tools.ModelPath is null)
        {
            errors.Add("未找到本地 Whisper 模型。");
        }

        if (microphones.Count == 0)
        {
            errors.Add("未检测到麦克风。");
        }

        return new VoiceStatus(tools, microphones, errors);
    }
}
