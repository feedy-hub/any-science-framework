using System.IO.Pipes;
using AnyVoice.Protocol;

namespace AnyVoice.Core;

public sealed class CompanionPipeServer
{
    private readonly string pipeName;
    private readonly Func<CompanionEvent, CancellationToken, Task> handler;
    private readonly TimeSpan clientTimeout;

    public CompanionPipeServer(
        string pipeName,
        Func<CompanionEvent, CancellationToken, Task> handler,
        TimeSpan? clientTimeout = null)
    {
        CompanionPipeNames.Validate(pipeName);
        ArgumentNullException.ThrowIfNull(handler);
        this.pipeName = pipeName;
        this.handler = handler;
        this.clientTimeout = clientTimeout ?? TimeSpan.FromSeconds(2);
        if (this.clientTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(clientTimeout));
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await HandleClientAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleClientAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(clientTimeout);
        CompanionAcknowledgement acknowledgement;
        try
        {
            var value = await PipeMessageCodec.ReadEventAsync(pipe, operation.Token).ConfigureAwait(false);
            var sanitized = value with
            {
                Text = value.Text is null ? null : SpeechTextSanitizer.Sanitize(value.Text),
            };
            await handler(sanitized, operation.Token).ConfigureAwait(false);
            acknowledgement = CompanionAcknowledgement.Success;
        }
        catch (CompanionProtocolException)
        {
            acknowledgement = new CompanionAcknowledgement(false, "invalid-event");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            acknowledgement = new CompanionAcknowledgement(false, "handler-error");
        }

        try
        {
            await CompanionAcknowledgementCodec.WriteAsync(pipe, acknowledgement, operation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            // The adapter may exit before reading a non-critical acknowledgement.
        }
    }
}
