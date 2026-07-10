using AnyVoice.Core;

namespace AnyVoice.Tests;

internal static class SettingsTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("settings defaults are safe", () =>
        {
            var value = CompanionSettings.Default;

            suite.Equal(1.0, value.Scale);
            suite.Equal(1.0, value.Opacity);
            suite.Equal(true, value.SubtitlesEnabled);
            suite.Equal(true, value.SpeechEnabled);
            suite.Equal(false, value.HotkeyEnabled);
            suite.Equal<string?>(null, value.AudioDevice);
        });

        suite.Test("settings normalization clamps values and trims paths", () =>
        {
            var value = new CompanionSettings
            {
                Scale = 9,
                Opacity = 0.1,
                SubtitleDurationSeconds = 300,
                WindowLeft = double.NaN,
                WindowTop = double.PositiveInfinity,
                FfmpegPath = "  C:\\tools\\ffmpeg.exe  ",
                WhisperPath = " ",
            }.Normalize();

            suite.Equal(2.0, value.Scale);
            suite.Equal(0.35, value.Opacity);
            suite.Equal(30, value.SubtitleDurationSeconds);
            suite.Equal<double?>(null, value.WindowLeft);
            suite.Equal<double?>(null, value.WindowTop);
            suite.Equal(@"C:\tools\ffmpeg.exe", value.FfmpegPath);
            suite.Equal<string?>(null, value.WhisperPath);
        });

        suite.Test("missing settings file returns defaults", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var store = new CompanionSettingsStore(Path.Combine(temporary.Path, "config.json"));

            suite.Equal(CompanionSettings.Default, store.Load());
        });

        suite.Test("settings round trip through UTF-8 JSON", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var path = Path.Combine(temporary.Path, "config.json");
            var store = new CompanionSettingsStore(path);
            var expected = CompanionSettings.Default with
            {
                Scale = 1.25,
                Opacity = 0.8,
                WindowLeft = 120,
                WindowTop = 240,
                AudioDevice = "麦克风 (USB Audio)",
            };

            store.Save(expected);
            var actual = store.Load();

            suite.Equal(expected, actual);
            suite.Equal(true, File.ReadAllText(path).Contains("麦克风", StringComparison.Ordinal));
            suite.Equal(0, Directory.GetFiles(temporary.Path, "*.tmp-*").Length);
        });

        suite.Test("malformed settings are quarantined", () =>
        {
            using var temporary = TemporaryDirectory.Create();
            var path = Path.Combine(temporary.Path, "config.json");
            File.WriteAllText(path, "{broken-json");
            var store = new CompanionSettingsStore(path);

            var actual = store.Load();

            suite.Equal(CompanionSettings.Default, actual);
            suite.Equal(false, File.Exists(path));
            suite.Equal(1, Directory.GetFiles(temporary.Path, "config.json.invalid-*").Length);
        });
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
                "anyvoice-tests",
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
