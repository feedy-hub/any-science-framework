using System.Buffers.Binary;
using System.Text;
using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class CodecTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("Unicode event round trips", async () =>
        {
            var expected = CompanionEvent.Create(CompanionEventType.Success, "codex", "任务完成");
            await using var stream = new MemoryStream();

            await PipeMessageCodec.WriteEventAsync(stream, expected);
            stream.Position = 0;
            var actual = await PipeMessageCodec.ReadEventAsync(stream);

            suite.Equal(expected, actual);
        });

        suite.TestAsync("zero length frame is rejected", async () =>
        {
            await using var stream = FrameWithDeclaredLength(0, []);

            await ThrowsAsync<CompanionProtocolException>(
                () => PipeMessageCodec.ReadEventAsync(stream));
        });

        suite.TestAsync("oversized frame is rejected before payload read", async () =>
        {
            await using var stream = FrameWithDeclaredLength(PipeMessageCodec.MaximumPayloadLength + 1, []);

            await ThrowsAsync<CompanionProtocolException>(
                () => PipeMessageCodec.ReadEventAsync(stream));
        });

        suite.TestAsync("truncated frame is rejected", async () =>
        {
            await using var stream = FrameWithDeclaredLength(5, [1, 2]);

            await ThrowsAsync<CompanionProtocolException>(
                () => PipeMessageCodec.ReadEventAsync(stream));
        });

        suite.TestAsync("malformed JSON is rejected without payload echo", async () =>
        {
            var payload = Encoding.UTF8.GetBytes("{not-json}");
            await using var stream = FrameWithDeclaredLength(payload.Length, payload);

            var exception = await ThrowsAsync<CompanionProtocolException>(
                () => PipeMessageCodec.ReadEventAsync(stream));

            suite.Equal(false, exception.Message.Contains("not-json", StringComparison.Ordinal));
        });
    }

    private static MemoryStream FrameWithDeclaredLength(int length, byte[] payload)
    {
        var stream = new MemoryStream();
        Span<byte> header = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        stream.Write(header);
        stream.Write(payload);
        stream.Position = 0;
        return stream;
    }

    private static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name}, got {exception.GetType().Name}.",
                exception);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }
}
