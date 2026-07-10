namespace AnyVoice.Core.Voice;

public sealed class VoiceStatusService
{
    private readonly VoiceToolDiscovery discovery;
    private readonly IProcessRunner processRunner;

    public VoiceStatusService(VoiceToolDiscovery discovery, IProcessRunner processRunner)
    {
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<VoiceStatus> InspectAsync(
        CompanionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var tools = discovery.Discover(settings);
        if (tools.FfmpegPath is null)
        {
            return VoiceStatus.From(tools, []);
        }

        IReadOnlyList<string> microphones = [];
        try
        {
            var result = await processRunner.RunAsync(
                    new ProcessRequest(
                        tools.FfmpegPath,
                        ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"],
                        null,
                        new Dictionary<string, string?>(),
                        TimeSpan.FromSeconds(10)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!result.TimedOut)
            {
                microphones = FfmpegDeviceParser.ParseMicrophones(
                    string.Concat(result.StandardError, Environment.NewLine, result.StandardOutput));
            }
        }
        catch (Exception exception) when (
            exception is VoiceOperationException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            // The readiness result below reports microphone discovery as unavailable.
        }

        return VoiceStatus.From(tools, microphones);
    }
}
