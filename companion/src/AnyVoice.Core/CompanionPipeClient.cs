using System.IO.Pipes;
using AnyVoice.Protocol;

namespace AnyVoice.Core;

public sealed class CompanionPipeClient
{
    private readonly string pipeName;
    private readonly TimeSpan connectTimeout;

    public CompanionPipeClient(string pipeName, TimeSpan? connectTimeout = null)
    {
        CompanionPipeNames.Validate(pipeName);
        this.pipeName = pipeName;
        this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(2);
        if (this.connectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectTimeout));
        }
    }

    public async Task<CompanionAcknowledgement> SendAsync(
        CompanionEvent value,
        CancellationToken cancellationToken = default)
    {
        CompanionEventValidator.Validate(value);
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(connectTimeout);

        try
        {
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await PipeMessageCodec.WriteEventAsync(pipe, value, timeout.Token).ConfigureAwait(false);
            return await CompanionAcknowledgementCodec.ReadAsync(pipe, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CompanionAcknowledgement.Unavailable;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CompanionProtocolException)
        {
            return CompanionAcknowledgement.Unavailable;
        }
    }
}
