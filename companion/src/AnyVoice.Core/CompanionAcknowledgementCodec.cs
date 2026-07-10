using System.Buffers.Binary;
using System.Text.Json;
using AnyVoice.Protocol;

namespace AnyVoice.Core;

internal static class CompanionAcknowledgementCodec
{
    private const int MaximumPayloadLength = 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    public static async Task WriteAsync(
        Stream stream,
        CompanionAcknowledgement value,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CompanionAcknowledgement> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > MaximumPayloadLength)
        {
            throw new CompanionProtocolException("Acknowledgement length is invalid.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<CompanionAcknowledgement>(payload, SerializerOptions)
                ?? throw new CompanionProtocolException("Acknowledgement payload is empty.");
        }
        catch (CompanionProtocolException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CompanionProtocolException("Acknowledgement payload is invalid.", exception);
        }
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new CompanionProtocolException("Acknowledgement frame is incomplete.");
            }

            totalRead += read;
        }
    }
}
