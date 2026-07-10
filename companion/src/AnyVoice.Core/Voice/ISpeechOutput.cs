namespace AnyVoice.Core.Voice;

public interface ISpeechOutput
{
    Task SpeakAsync(string? text, CancellationToken cancellationToken = default);
}
