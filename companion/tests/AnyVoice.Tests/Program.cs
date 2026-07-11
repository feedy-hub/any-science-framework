namespace AnyVoice.Tests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var requestedGroup = args.Length == 0 ? "all" : args[0].Trim().ToLowerInvariant();
        var suite = new TestSuite();

        if (requestedGroup is "all" or "protocol")
        {
            ProtocolTests.Register(suite);
        }

        if (requestedGroup is "all" or "sanitizer")
        {
            SanitizerTests.Register(suite);
        }

        if (requestedGroup is "all" or "codec")
        {
            CodecTests.Register(suite);
        }

        if (requestedGroup is "all" or "pipe")
        {
            PipeTransportTests.Register(suite);
        }

        if (requestedGroup is "all" or "desktop")
        {
            LocalPathTests.Register(suite);
            DesktopControllerTests.Register(suite);
        }

        if (requestedGroup is "all" or "settings")
        {
            SettingsTests.Register(suite);
        }

        if (requestedGroup is "all" or "single-instance")
        {
            SingleInstanceTests.Register(suite);
        }

        if (requestedGroup is "all" or "startup")
        {
            StartupRegistrationTests.Register(suite);
        }

        if (requestedGroup is "all" or "desktop-settings")
        {
            DesktopSettingsTests.Register(suite);
        }

        if (requestedGroup is "all" or "voice-discovery")
        {
            VoiceDiscoveryTests.Register(suite);
        }

        if (requestedGroup is "all" or "speech")
        {
            SpeechOutputTests.Register(suite);
            SpeechCoordinatorTests.Register(suite);
        }

        if (requestedGroup is "all" or "whisper")
        {
            WhisperTranscriberTests.Register(suite);
        }

        if (requestedGroup is "all" or "dictation")
        {
            DictationControllerTests.Register(suite);
        }

        if (suite.RegisteredCount == 0)
        {
            Console.Error.WriteLine($"Unknown or empty test group: {requestedGroup}");
            return 2;
        }

        return await suite.RunAsync().ConfigureAwait(false);
    }
}
