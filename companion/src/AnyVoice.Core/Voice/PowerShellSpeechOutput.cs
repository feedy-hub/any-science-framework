using AnyVoice.Protocol;

namespace AnyVoice.Core.Voice;

public sealed class PowerShellSpeechOutput : ISpeechOutput
{
    private const string SpeechCommand = """
        $ErrorActionPreference = 'Stop'
        $text = [Console]::In.ReadToEnd()
        Add-Type -AssemblyName System.Speech
        $speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new()
        try { $speaker.Speak($text) } finally { $speaker.Dispose() }
        """;

    private readonly IProcessRunner processRunner;
    private readonly string powershellPath;

    public PowerShellSpeechOutput(IProcessRunner processRunner, string powershellPath = "powershell.exe")
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentException.ThrowIfNullOrWhiteSpace(powershellPath);
        this.powershellPath = powershellPath;
    }

    public async Task SpeakAsync(string? text, CancellationToken cancellationToken = default)
    {
        var sanitized = SpeechTextSanitizer.Sanitize(text);
        if (sanitized.Length == 0)
        {
            return;
        }

        var request = new ProcessRequest(
            powershellPath,
            ["-NoProfile", "-NonInteractive", "-Command", SpeechCommand],
            sanitized,
            new Dictionary<string, string?>(),
            TimeSpan.FromMinutes(2));
        var result = await processRunner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
        {
            throw new VoiceOperationException("Speech output timed out.");
        }

        if (result.ExitCode != 0)
        {
            throw new VoiceOperationException("Speech output failed.");
        }
    }
}
