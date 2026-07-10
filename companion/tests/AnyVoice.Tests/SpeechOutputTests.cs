using AnyVoice.Core.Voice;

namespace AnyVoice.Tests;

internal static class SpeechOutputTests
{
    public static void Register(TestSuite suite)
    {
        suite.TestAsync("speech text is sanitized and sent only through stdin", async () =>
        {
            var runner = new RecordingProcessRunner();
            var output = new PowerShellSpeechOutput(runner, "powershell.exe");

            await output.SpeakAsync(@"Done token=secret C:\Users\PS\private.txt");

            suite.Equal(1, runner.Requests.Count);
            var request = runner.Requests[0];
            suite.Equal("Done token=[redacted] [path]", request.StandardInput);
            suite.Equal(false, request.Arguments.Any(value => value.Contains("secret", StringComparison.Ordinal)));
            suite.Equal(false, request.Arguments.Any(value => value.Contains("private.txt", StringComparison.Ordinal)));
        });

        suite.TestAsync("empty safe speech does not start a process", async () =>
        {
            var runner = new RecordingProcessRunner();
            var output = new PowerShellSpeechOutput(runner, "powershell.exe");

            await output.SpeakAsync("```text\nsecret\n```");

            suite.Equal(0, runner.Requests.Count);
        });

        suite.TestAsync("speech process failure is reported", async () =>
        {
            var runner = new RecordingProcessRunner
            {
                Result = new ProcessResult(1, string.Empty, "speech failed", false),
            };
            var output = new PowerShellSpeechOutput(runner, "powershell.exe");

            await ThrowsAsync<VoiceOperationException>(() => output.SpeakAsync("Done"));
        });

        suite.TestAsync("process runner captures output", async () =>
        {
            var runner = new ProcessRunner();
            var request = new ProcessRequest(
                "powershell.exe",
                ["-NoProfile", "-NonInteractive", "-Command", "[Console]::Out.Write('ok')"],
                null,
                new Dictionary<string, string?>(),
                TimeSpan.FromSeconds(5));

            var result = await runner.RunAsync(request);

            suite.Equal(0, result.ExitCode);
            suite.Equal("ok", result.StandardOutput);
            suite.Equal(false, result.TimedOut);
        });

        suite.TestAsync("process runner enforces timeout", async () =>
        {
            var runner = new ProcessRunner();
            var request = new ProcessRequest(
                "powershell.exe",
                ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 5"],
                null,
                new Dictionary<string, string?>(),
                TimeSpan.FromMilliseconds(150));

            var result = await runner.RunAsync(request).WaitAsync(TimeSpan.FromSeconds(2));

            suite.Equal(true, result.TimedOut);
        });

        suite.TestAsync("process runner wraps executable start failures", async () =>
        {
            var runner = new ProcessRunner();
            var request = new ProcessRequest(
                $"anyvoice-missing-{Guid.NewGuid():N}.exe",
                [],
                null,
                new Dictionary<string, string?>(),
                TimeSpan.FromSeconds(1));

            await ThrowsAsync<VoiceOperationException>(() => runner.RunAsync(request));
        });
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

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public ProcessResult Result { get; init; } = new(0, string.Empty, string.Empty, false);

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }
}
