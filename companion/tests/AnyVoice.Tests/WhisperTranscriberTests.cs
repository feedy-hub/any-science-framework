using AnyVoice.Core.Voice;

namespace AnyVoice.Tests;

internal static class WhisperTranscriberTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("Whisper uses explicit cached model and contained output", async () =>
        {
            using var temporary = TemporaryDirectory.Create("anyvoice whisper test");
            var whisper = temporary.CreateFile("tools/whisper.exe", "exe");
            var model = temporary.CreateFile("model cache/tiny.pt", "model");
            var audio = temporary.CreateFile("audio input/sample.wav", "wav");
            var runner = new RecordingProcessRunner(request =>
            {
                var outputIndex = request.Arguments.ToList().IndexOf("--output_dir");
                var outputDirectory = request.Arguments[outputIndex + 1];
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "sample.txt"), "测试转写\n");
                return new ProcessResult(0, string.Empty, string.Empty, false);
            });
            var transcriber = new WhisperTranscriber(runner, temporary.Path);

            var transcript = await transcriber.TranscribeAsync(
                audio,
                new VoiceToolPaths(null, whisper, model));

            suite.Equal("测试转写", transcript);
            var request = runner.Requests.Single();
            suite.Equal(audio, request.Arguments[0]);
            suite.Equal("tiny", ValueAfter(request.Arguments, "--model"));
            suite.Equal(Path.GetDirectoryName(model), ValueAfter(request.Arguments, "--model_dir"));
            suite.Equal("1", request.Environment["HF_HUB_OFFLINE"]);
            suite.Equal("1", request.Environment["TRANSFORMERS_OFFLINE"]);
            suite.Equal(0, Directory.GetDirectories(temporary.Path, "anyvoice-whisper-*").Length);
        });

        suite.TestAsync("missing cached model is rejected before process start", async () =>
        {
            using var temporary = TemporaryDirectory.Create("anyvoice whisper missing");
            var whisper = temporary.CreateFile("whisper.exe", "exe");
            var audio = temporary.CreateFile("sample.wav", "wav");
            var runner = new RecordingProcessRunner(_ => new ProcessResult(0, "", "", false));
            var transcriber = new WhisperTranscriber(runner, temporary.Path);

            await ThrowsAsync<VoiceOperationException>(() => transcriber.TranscribeAsync(
                audio,
                new VoiceToolPaths(null, whisper, null)));

            suite.Equal(0, runner.Requests.Count);
        });

        suite.TestAsync("Whisper non-zero exit is reported and output is cleaned", async () =>
        {
            using var temporary = TemporaryDirectory.Create("anyvoice whisper failure");
            var whisper = temporary.CreateFile("whisper.exe", "exe");
            var model = temporary.CreateFile("tiny.pt", "model");
            var audio = temporary.CreateFile("sample.wav", "wav");
            var runner = new RecordingProcessRunner(_ => new ProcessResult(2, "", "failure", false));
            var transcriber = new WhisperTranscriber(runner, temporary.Path);

            await ThrowsAsync<VoiceOperationException>(() => transcriber.TranscribeAsync(
                audio,
                new VoiceToolPaths(null, whisper, model)));

            suite.Equal(0, Directory.GetDirectories(temporary.Path, "anyvoice-whisper-*").Length);
        });
    }

    private static string ValueAfter(IReadOnlyList<string> values, string name)
    {
        var index = values.ToList().IndexOf(name);
        return index >= 0 ? values[index + 1] : throw new InvalidOperationException($"Missing {name}.");
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

    private sealed class RecordingProcessRunner(
        Func<ProcessRequest, ProcessResult> handler) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create(string prefix)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                prefix,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, relativePath));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
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
