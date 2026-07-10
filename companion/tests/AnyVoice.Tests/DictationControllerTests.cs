using AnyVoice.Core.Voice;
using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class DictationControllerTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("dictation toggles from recording through transcription", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var recorder = new FakeRecorder(createAudio: true);
            var transcriber = new FakeTranscriber("hello world");
            var events = new List<CompanionEvent>();
            var transcripts = new List<string>();
            var controller = CreateController(temporary.Path, recorder, transcriber);
            controller.StateEvent += (_, value) => events.Add(value);
            controller.TranscriptReady += (_, value) => transcripts.Add(value);

            await controller.ToggleAsync();
            suite.Equal(DictationState.Recording, controller.State);
            suite.Equal(CompanionEventType.Listening, events[^1].Type);

            await controller.ToggleAsync();
            suite.Equal(DictationState.Idle, controller.State);
            suite.Equal(1, recorder.StartCount);
            suite.Equal(1, recorder.StopCount);
            suite.Equal(1, transcriber.CallCount);
            suite.Equal("hello world", transcripts.Single());
            suite.Equal(CompanionEventType.Success, events[^1].Type);
            suite.Equal(0, Directory.GetFiles(temporary.Path, "*.wav").Length);
        });

        suite.TestAsync("recorder failure returns to idle with error event", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var recorder = new FakeRecorder(createAudio: false)
            {
                StartException = new VoiceOperationException("recorder failed"),
            };
            var events = new List<CompanionEvent>();
            var controller = CreateController(
                temporary.Path,
                recorder,
                new FakeTranscriber("unused"));
            controller.StateEvent += (_, value) => events.Add(value);

            await controller.ToggleAsync();

            suite.Equal(DictationState.Idle, controller.State);
            suite.Equal(CompanionEventType.Error, events.Single().Type);
        });

        suite.TestAsync("transcriber failure cleans audio and reports error", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var recorder = new FakeRecorder(createAudio: true);
            var transcriber = new FakeTranscriber("unused")
            {
                Exception = new VoiceOperationException("transcriber failed"),
            };
            var events = new List<CompanionEvent>();
            var controller = CreateController(temporary.Path, recorder, transcriber);
            controller.StateEvent += (_, value) => events.Add(value);

            await controller.ToggleAsync();
            await controller.ToggleAsync();

            suite.Equal(DictationState.Idle, controller.State);
            suite.Equal(CompanionEventType.Error, events[^1].Type);
            suite.Equal(0, Directory.GetFiles(temporary.Path, "*.wav").Length);
        });

        suite.TestAsync("re-entrant dictation toggle is rejected", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recorder = new FakeRecorder(createAudio: true) { StartGate = gate.Task };
            var controller = CreateController(
                temporary.Path,
                recorder,
                new FakeTranscriber("unused"));
            var first = controller.ToggleAsync();

            await ThrowsAsync<VoiceOperationException>(() => controller.ToggleAsync());
            gate.SetResult();
            await first;

            suite.Equal(DictationState.Recording, controller.State);
        });

        suite.TestAsync("cancelled recorder start returns to idle", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var controller = CreateController(
                temporary.Path,
                new CancellingRecorder(),
                new FakeTranscriber("unused"));

            await ThrowsAsync<OperationCanceledException>(
                () => controller.ToggleAsync(cancellation.Token));

            suite.Equal(DictationState.Idle, controller.State);
            suite.Equal(0, Directory.GetFiles(temporary.Path, "*.wav").Length);
        });
    }

    private static DictationController CreateController(
        string temporaryPath,
        IAudioRecorder recorder,
        ITranscriber transcriber)
    {
        var ffmpeg = Path.Combine(temporaryPath, "ffmpeg.exe");
        var whisper = Path.Combine(temporaryPath, "whisper.exe");
        var model = Path.Combine(temporaryPath, "tiny.pt");
        File.WriteAllText(ffmpeg, "ffmpeg");
        File.WriteAllText(whisper, "whisper");
        File.WriteAllText(model, "model");
        var tools = new VoiceToolPaths(ffmpeg, whisper, model);
        return new DictationController(
            recorder,
            transcriber,
            () => tools,
            () => "Microphone (USB Audio)",
            temporaryPath,
            retainAudio: false);
    }

    private static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private sealed class FakeRecorder(bool createAudio) : IAudioRecorder
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Exception? StartException { get; init; }

        public Task? StartGate { get; init; }

        public async Task StartAsync(
            string ffmpegPath,
            string microphone,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            if (StartGate is not null)
            {
                await StartGate;
            }

            if (StartException is not null)
            {
                throw StartException;
            }

            if (createAudio)
            {
                File.WriteAllText(outputPath, "wav");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTranscriber(string transcript) : ITranscriber
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; init; }

        public Task<string> TranscribeAsync(
            string audioPath,
            VoiceToolPaths tools,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(transcript);
        }
    }

    private sealed class CancellingRecorder : IAudioRecorder
    {
        public Task StartAsync(
            string ffmpegPath,
            string microphone,
            string outputPath,
            CancellationToken cancellationToken = default)
        {
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "anyvoice-dictation-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
