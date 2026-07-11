using AnyVoice.Protocol;

namespace AnyVoice.Core.Voice;

public sealed class DictationController
{
    private readonly IAudioRecorder recorder;
    private readonly ITranscriber transcriber;
    private readonly Func<VoiceToolPaths> toolsProvider;
    private readonly Func<string?> microphoneProvider;
    private readonly string temporaryPath;
    private readonly bool retainAudio;
    private int operationActive;
    private string? recordingPath;

    public DictationController(
        IAudioRecorder recorder,
        ITranscriber transcriber,
        Func<VoiceToolPaths> toolsProvider,
        Func<string?> microphoneProvider,
        string temporaryPath,
        bool retainAudio)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        this.toolsProvider = toolsProvider ?? throw new ArgumentNullException(nameof(toolsProvider));
        this.microphoneProvider = microphoneProvider
            ?? throw new ArgumentNullException(nameof(microphoneProvider));
        this.temporaryPath = Path.GetFullPath(temporaryPath);
        this.retainAudio = retainAudio;
    }

    public event EventHandler<CompanionEvent>? StateEvent;

    public event EventHandler<string>? TranscriptReady;

    public DictationState State { get; private set; } = DictationState.Idle;

    public async Task ToggleAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref operationActive, 1, 0) != 0)
        {
            throw new VoiceOperationException("A dictation operation is already in progress.");
        }

        try
        {
            if (State == DictationState.Idle)
            {
                await StartRecordingAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (State == DictationState.Recording)
            {
                await StopAndTranscribeAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new VoiceOperationException("Dictation is already being transcribed.");
            }
        }
        catch (OperationCanceledException)
        {
            State = DictationState.Idle;
            CleanupRecording();
            throw;
        }
        catch (Exception exception) when (IsOperationalFailure(exception))
        {
            State = DictationState.Idle;
            CleanupRecording();
            Emit(CompanionEventType.Error, "听写失败。");
        }
        finally
        {
            Volatile.Write(ref operationActive, 0);
        }
    }

    private async Task StartRecordingAsync(CancellationToken cancellationToken)
    {
        var tools = toolsProvider();
        var ffmpegPath = RequireFile(tools.FfmpegPath, "FFmpeg executable is missing.");
        var microphone = microphoneProvider();
        if (string.IsNullOrWhiteSpace(microphone))
        {
            throw new VoiceOperationException("A microphone has not been selected.");
        }

        Directory.CreateDirectory(temporaryPath);
        recordingPath = Path.Combine(
            temporaryPath,
            $"anyvoice-recording-{Guid.NewGuid():N}.wav");
        await recorder.StartAsync(
                ffmpegPath,
                microphone.Trim(),
                recordingPath,
                cancellationToken)
            .ConfigureAwait(false);
        State = DictationState.Recording;
        Emit(CompanionEventType.Listening, "正在聆听。");
    }

    private async Task StopAndTranscribeAsync(CancellationToken cancellationToken)
    {
        var audioPath = recordingPath
            ?? throw new VoiceOperationException("No active recording was found.");
        State = DictationState.Transcribing;
        Emit(CompanionEventType.Thinking, "正在本地转写。");

        await recorder.StopAsync(cancellationToken).ConfigureAwait(false);
        var transcript = (await transcriber.TranscribeAsync(
                audioPath,
                toolsProvider(),
                cancellationToken)
            .ConfigureAwait(false)).Trim();
        if (transcript.Length == 0)
        {
            throw new VoiceOperationException("Whisper produced an empty transcript.");
        }

        TranscriptReady?.Invoke(this, transcript);
        Emit(CompanionEventType.Success, "听写完成，内容已复制到剪贴板。");
        State = DictationState.Idle;
        CleanupRecording();
    }

    private void Emit(CompanionEventType type, string text)
    {
        StateEvent?.Invoke(this, CompanionEvent.Create(type, "dictation", text));
    }

    private static string RequireFile(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VoiceOperationException(message);
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                throw new VoiceOperationException(message);
            }

            return fullPath;
        }
        catch (VoiceOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or UnauthorizedAccessException)
        {
            throw new VoiceOperationException(message, exception);
        }
    }

    private void CleanupRecording()
    {
        var path = recordingPath;
        recordingPath = null;
        if (retainAudio || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = temporaryPath
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(fullPath).StartsWith(
                    "anyvoice-recording-",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetExtension(fullPath), ".wav", StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or NotSupportedException
                or UnauthorizedAccessException)
        {
            // Cleanup is best-effort and constrained to AnyVoice temporary recordings.
        }
    }

    private static bool IsOperationalFailure(Exception exception)
    {
        return exception is VoiceOperationException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException;
    }
}
