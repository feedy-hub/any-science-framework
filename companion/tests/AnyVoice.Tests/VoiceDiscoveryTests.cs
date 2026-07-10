using AnyVoice.Core;
using AnyVoice.Core.Voice;

namespace AnyVoice.Tests;

internal static class VoiceDiscoveryTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("explicit voice paths take precedence", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var explicitFfmpeg = temporary.CreateFile("explicit/ffmpeg.exe", "ffmpeg");
            var explicitWhisper = temporary.CreateFile("explicit/whisper.exe", "whisper");
            var explicitModel = temporary.CreateFile("models/tiny.pt", "model");
            temporary.CreateFile("path/ffmpeg.exe", "other");
            var discovery = new VoiceToolDiscovery(
                Path.Combine(temporary.Path, "path"),
                temporary.Path);

            var paths = discovery.Discover(new CompanionSettings
            {
                FfmpegPath = explicitFfmpeg,
                WhisperPath = explicitWhisper,
                WhisperModelPath = explicitModel,
            });

            suite.Equal(explicitFfmpeg, paths.FfmpegPath);
            suite.Equal(explicitWhisper, paths.WhisperPath);
            suite.Equal(explicitModel, paths.ModelPath);
        });

        suite.Test("voice executables are discovered from PATH", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var bin = Path.Combine(temporary.Path, "bin");
            var ffmpeg = temporary.CreateFile("bin/ffmpeg.exe", "ffmpeg");
            var whisper = temporary.CreateFile("bin/whisper.exe", "whisper");
            var discovery = new VoiceToolDiscovery(bin, temporary.Path);

            var paths = discovery.Discover(CompanionSettings.Default);

            suite.Equal(ffmpeg, paths.FfmpegPath);
            suite.Equal(whisper, paths.WhisperPath);
        });

        suite.Test("complete cached model is selected and partial file is ignored", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var cache = Path.Combine(temporary.Path, ".cache", "whisper");
            Directory.CreateDirectory(cache);
            File.WriteAllBytes(Path.Combine(cache, "turbo.pt"), []);
            var tiny = temporary.CreateFile(".cache/whisper/tiny.pt", "cached-model");
            var discovery = new VoiceToolDiscovery(string.Empty, temporary.Path);

            var paths = discovery.Discover(CompanionSettings.Default);

            suite.Equal(tiny, paths.ModelPath);
        });

        suite.Test("missing tools produce actionable status", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var discovery = new VoiceToolDiscovery(string.Empty, temporary.Path);
            var paths = discovery.Discover(CompanionSettings.Default);

            var status = VoiceStatus.From(paths, []);

            suite.Equal(false, status.IsReady);
            suite.Equal(true, status.Errors.Contains("FFmpeg not found."));
            suite.Equal(true, status.Errors.Contains("Whisper not found."));
            suite.Equal(true, status.Errors.Contains("Cached Whisper model not found."));
        });

        suite.Test("FFmpeg DirectShow microphones are parsed", () =>
        {
            const string output = """
                [dshow @ 0001] "麦克风 (AB13X USB Audio)" (audio)
                [dshow @ 0001] Alternative name "@device_cm_{ABC}"
                [dshow @ 0001] "Stereo Mix (Realtek Audio)" (audio)
                [dshow @ 0001] "Integrated Camera" (video)
                [dshow @ 0001] "麦克风 (AB13X USB Audio)" (audio)
                """;

            var devices = FfmpegDeviceParser.ParseMicrophones(output);

            suite.Equal(2, devices.Count);
            suite.Equal("麦克风 (AB13X USB Audio)", devices[0]);
            suite.Equal("Stereo Mix (Realtek Audio)", devices[1]);
        });

        suite.TestAsync("voice status service enumerates microphones without requiring ffmpeg success", async () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var bin = Path.Combine(temporary.Path, "bin");
            temporary.CreateFile("bin/ffmpeg.exe", "ffmpeg");
            temporary.CreateFile("bin/whisper.exe", "whisper");
            temporary.CreateFile(".cache/whisper/tiny.pt", "model");
            var runner = new FakeProcessRunner(new ProcessResult(
                1,
                string.Empty,
                "[dshow @ 0001] \"USB Microphone\" (audio)",
                false));
            var service = new VoiceStatusService(
                new VoiceToolDiscovery(bin, temporary.Path),
                runner);

            var status = await service.InspectAsync(CompanionSettings.Default);

            suite.Equal(true, status.IsReady);
            suite.Equal("USB Microphone", status.Microphones.Single());
            suite.Equal("-list_devices", runner.Request!.Arguments[1]);
            suite.Equal("dshow", runner.Request.Arguments[4]);
        });
    }

    private sealed class FakeProcessRunner(ProcessResult result) : IProcessRunner
    {
        public ProcessRequest? Request { get; private set; }

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
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
                "anyvoice-voice-tests",
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
