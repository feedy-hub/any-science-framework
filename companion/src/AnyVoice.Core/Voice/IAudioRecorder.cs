namespace AnyVoice.Core.Voice;

public interface IAudioRecorder : IAsyncDisposable
{
    Task StartAsync(
        string ffmpegPath,
        string microphone,
        string outputPath,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
