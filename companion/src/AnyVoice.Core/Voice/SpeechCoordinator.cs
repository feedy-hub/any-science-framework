using AnyVoice.Protocol;

namespace AnyVoice.Core.Voice;

public sealed class SpeechCoordinator
{
    private readonly ISpeechOutput speechOutput;
    private readonly Func<bool> isEnabled;
    private readonly SemaphoreSlim speechGate = new(1, 1);

    public SpeechCoordinator(ISpeechOutput speechOutput, Func<bool> isEnabled)
    {
        this.speechOutput = speechOutput
            ?? throw new ArgumentNullException(nameof(speechOutput));
        this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
    }

    public async Task NotifyAsync(
        CompanionEvent value,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSpeak(value))
        {
            return;
        }

        await speechGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!ShouldSpeak(value))
            {
                return;
            }

            await speechOutput.SpeakAsync(value.Text, cancellationToken).ConfigureAwait(false);
        }
        catch (VoiceOperationException)
        {
            // Speech is optional and must never interrupt the desktop event channel.
        }
        finally
        {
            speechGate.Release();
        }
    }

    private bool ShouldSpeak(CompanionEvent value)
    {
        return isEnabled()
            && !string.Equals(value.Source, "dictation", StringComparison.OrdinalIgnoreCase)
            && value.Type is CompanionEventType.Success
                or CompanionEventType.NeedsInput
                or CompanionEventType.Error;
    }
}
