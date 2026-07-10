using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnyVoice.Protocol;

public static class PipeMessageCodec
{
    public const int MaximumPayloadLength = 65_536;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public static async Task WriteEventAsync(
        Stream stream,
        CompanionEvent value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CompanionEventValidator.Validate(value);

        var payload = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        if (payload.Length is <= 0 or > MaximumPayloadLength)
        {
            throw new CompanionProtocolException("Serialized event has an invalid size.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CompanionEvent> ReadEventAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength is <= 0 or > MaximumPayloadLength)
        {
            throw new CompanionProtocolException("Frame length is outside the allowed range.");
        }

        var payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);

        try
        {
            var value = JsonSerializer.Deserialize<CompanionEvent>(payload, SerializerOptions)
                ?? throw new CompanionProtocolException("Event payload is empty.");
            CompanionEventValidator.Validate(value);
            return value;
        }
        catch (CompanionProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new CompanionProtocolException("Event payload is not valid JSON.", exception);
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
                throw new CompanionProtocolException("Frame ended before the declared length.");
            }

            totalRead += read;
        }
    }
}
