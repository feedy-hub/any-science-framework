using AnyVoice.Core;
using AnyVoice.Protocol;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AnyVoice.Tests;

internal static class PipeTransportTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("pipe round trip sanitizes text and acknowledges", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            var received = new TaskCompletionSource<CompanionEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var server = new CompanionPipeServer(
                pipeName,
                (value, _) =>
                {
                    received.TrySetResult(value);
                    return Task.CompletedTask;
                });
            using var cancellation = new CancellationTokenSource();
            var serverTask = server.RunAsync(cancellation.Token);
            var client = new CompanionPipeClient(pipeName, TimeSpan.FromSeconds(2));

            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Success, "codex", "Done token=secret123"));
            var actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

            suite.Equal(true, acknowledgement.Accepted);
            suite.Equal<string?>(null, acknowledgement.Error);
            suite.Equal("Done token=[redacted]", actual.Text);

            cancellation.Cancel();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
        });

        suite.TestAsync("server cancellation exits promptly", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            var server = new CompanionPipeServer(pipeName, (_, _) => Task.CompletedTask);
            using var cancellation = new CancellationTokenSource();
            var serverTask = server.RunAsync(cancellation.Token);

            cancellation.Cancel();

            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            suite.Equal(true, serverTask.IsCompletedSuccessfully);
        });

        suite.TestAsync("missing companion returns unavailable acknowledgement", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            var client = new CompanionPipeClient(pipeName, TimeSpan.FromMilliseconds(100));

            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Thinking, "manual", "hello"));

            suite.Equal(false, acknowledgement.Accepted);
            suite.Equal("companion-unavailable", acknowledgement.Error);
        });

        suite.TestAsync("client times out when connected peer never acknowledges", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            await using var rawServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var hold = new CancellationTokenSource();
            var serverTask = Task.Run(async () =>
            {
                await rawServer.WaitForConnectionAsync(hold.Token);
                await Task.Delay(TimeSpan.FromSeconds(5), hold.Token);
            });
            var client = new CompanionPipeClient(pipeName, TimeSpan.FromMilliseconds(150));

            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Thinking, "manual", "hello"))
                .WaitAsync(TimeSpan.FromSeconds(1));

            suite.Equal(false, acknowledgement.Accepted);
            suite.Equal("companion-unavailable", acknowledgement.Error);
            hold.Cancel();
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException)
            {
            }
        });

        suite.TestAsync("stalled client does not block the next event", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            var received = new TaskCompletionSource<CompanionEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var server = new CompanionPipeServer(
                pipeName,
                (value, _) =>
                {
                    received.TrySetResult(value);
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(150));
            using var cancellation = new CancellationTokenSource();
            var serverTask = server.RunAsync(cancellation.Token);
            await using var stalledClient = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await stalledClient.ConnectAsync(1000);
            await Task.Delay(300);

            var client = new CompanionPipeClient(pipeName, TimeSpan.FromSeconds(1));
            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Success, "manual", "recovered"));

            suite.Equal(true, acknowledgement.Accepted);
            suite.Equal("recovered", (await received.Task.WaitAsync(TimeSpan.FromSeconds(1))).Text);
            cancellation.Cancel();
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
        });

        suite.TestAsync("invalid pipe frames are rejected and server recovers", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            var received = new TaskCompletionSource<CompanionEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var server = new CompanionPipeServer(
                pipeName,
                (value, _) =>
                {
                    received.TrySetResult(value);
                    return Task.CompletedTask;
                });
            using var cancellation = new CancellationTokenSource();
            var serverTask = server.RunAsync(cancellation.Token);
            try
            {
                var malformed = Encoding.UTF8.GetBytes("{not-json}");
                var malformedAck = await SendRawFrameAsync(pipeName, malformed.Length, malformed);
                suite.Equal(false, malformedAck.Accepted);
                suite.Equal("invalid-event", malformedAck.Error);

                var oversizedAck = await SendRawFrameAsync(
                    pipeName,
                    PipeMessageCodec.MaximumPayloadLength + 1,
                    []);
                suite.Equal(false, oversizedAck.Accepted);
                suite.Equal("invalid-event", oversizedAck.Error);

                var client = new CompanionPipeClient(pipeName, TimeSpan.FromSeconds(1));
                var recoveredAck = await client.SendAsync(
                    CompanionEvent.Create(CompanionEventType.Success, "manual", "recovered"));
                suite.Equal(true, recoveredAck.Accepted);
                suite.Equal("recovered", (await received.Task.WaitAsync(TimeSpan.FromSeconds(1))).Text);
            }
            finally
            {
                cancellation.Cancel();
                await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
        });

        suite.TestAsync("peer disconnect after connect returns unavailable", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            await using var rawServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var serverTask = Task.Run(async () =>
            {
                await rawServer.WaitForConnectionAsync();
                rawServer.Disconnect();
            });
            var client = new CompanionPipeClient(pipeName, TimeSpan.FromSeconds(1));

            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Thinking, "manual", "hello"));

            suite.Equal(false, acknowledgement.Accepted);
            suite.Equal("companion-unavailable", acknowledgement.Error);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(1));
        });

        suite.TestAsync("malformed acknowledgement returns unavailable", async () =>
        {
            var pipeName = CompanionPipeNames.ForTests(Guid.NewGuid().ToString("N"));
            await using var rawServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var serverTask = Task.Run(async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await rawServer.WaitForConnectionAsync(timeout.Token);
                var eventHeader = new byte[sizeof(int)];
                await ReadExactlyAsync(rawServer, eventHeader, timeout.Token);
                var eventLength = BinaryPrimitives.ReadInt32LittleEndian(eventHeader);
                var eventPayload = new byte[eventLength];
                await ReadExactlyAsync(rawServer, eventPayload, timeout.Token);

                var invalidAcknowledgement = Encoding.UTF8.GetBytes("{bad}");
                BinaryPrimitives.WriteInt32LittleEndian(eventHeader, invalidAcknowledgement.Length);
                await rawServer.WriteAsync(eventHeader, timeout.Token);
                await rawServer.WriteAsync(invalidAcknowledgement, timeout.Token);
                await rawServer.FlushAsync(timeout.Token);
            });
            var client = new CompanionPipeClient(pipeName, TimeSpan.FromSeconds(1));

            var acknowledgement = await client.SendAsync(
                CompanionEvent.Create(CompanionEventType.Thinking, "manual", "hello"));

            suite.Equal(false, acknowledgement.Accepted);
            suite.Equal("companion-unavailable", acknowledgement.Error);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
        });

        suite.Test("production pipe name does not expose account identity", () =>
        {
            var pipeName = CompanionPipeNames.ForCurrentUser();
            var userName = Environment.UserName;

            suite.Equal(true, pipeName.StartsWith("AnyVoiceCompanion-", StringComparison.Ordinal));
            suite.Equal(false, pipeName.Contains(userName, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static async Task<TestAcknowledgement> SendRawFrameAsync(
        string pipeName,
        int declaredLength,
        byte[] payload)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await client.ConnectAsync(timeout.Token);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, declaredLength);
        await client.WriteAsync(header, timeout.Token);
        if (payload.Length > 0)
        {
            await client.WriteAsync(payload, timeout.Token);
        }

        await client.FlushAsync(timeout.Token);
        await ReadExactlyAsync(client, header, timeout.Token);
        var acknowledgementLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (acknowledgementLength is <= 0 or > 1024)
        {
            throw new InvalidOperationException("Invalid acknowledgement length in integration test.");
        }

        var acknowledgementPayload = new byte[acknowledgementLength];
        await ReadExactlyAsync(client, acknowledgementPayload, timeout.Token);
        return JsonSerializer.Deserialize<TestAcknowledgement>(
            acknowledgementPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Missing acknowledgement in integration test.");
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Integration-test pipe ended early.");
            }

            totalRead += read;
        }
    }

    private sealed record TestAcknowledgement(bool Accepted, string? Error);
}
