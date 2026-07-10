namespace AnyVoice.Core.Voice;

public interface ITranscriber
{
    Task<string> TranscribeAsync(
        string audioPath,
        VoiceToolPaths tools,
        CancellationToken cancellationToken = default);
}
