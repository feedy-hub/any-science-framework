using System.Text;

namespace AnyVoice.Core.Voice;

public sealed class WhisperTranscriber : ITranscriber
{
    private readonly IProcessRunner processRunner;
    private readonly string temporaryRoot;

    public WhisperTranscriber(IProcessRunner processRunner, string? temporaryRoot = null)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.temporaryRoot = Path.GetFullPath(temporaryRoot ?? Path.GetTempPath());
    }

    public async Task<string> TranscribeAsync(
        string audioPath,
        VoiceToolPaths tools,
        CancellationToken cancellationToken = default)
    {
        var audio = RequireCompleteFile(audioPath, "Audio file is missing.");
        var whisper = RequireCompleteFile(tools.WhisperPath, "Whisper executable is missing.");
        var model = RequireCompleteFile(tools.ModelPath, "Cached Whisper model is missing.");
        if (!string.Equals(Path.GetExtension(model), ".pt", StringComparison.OrdinalIgnoreCase))
        {
            throw new VoiceOperationException("Cached Whisper model must be a .pt file.");
        }

        var outputDirectory = Path.Combine(
            temporaryRoot,
            $"anyvoice-whisper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            var arguments = new[]
            {
                audio,
                "--model",
                Path.GetFileNameWithoutExtension(model),
                "--model_dir",
                Path.GetDirectoryName(model)!,
                "--output_format",
                "txt",
                "--output_dir",
                outputDirectory,
            };
            var environment = new Dictionary<string, string?>
            {
                ["HF_HUB_OFFLINE"] = "1",
                ["TRANSFORMERS_OFFLINE"] = "1",
                ["PYTHONUTF8"] = "1",
                ["PYTHONIOENCODING"] = "utf-8",
            };
            var result = await processRunner.RunAsync(
                    new ProcessRequest(
                        whisper,
                        arguments,
                        null,
                        environment,
                        TimeSpan.FromMinutes(20)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.TimedOut)
            {
                throw new VoiceOperationException("Whisper transcription timed out.");
            }

            if (result.ExitCode != 0)
            {
                throw new VoiceOperationException("Whisper transcription failed.");
            }

            var transcriptPath = Path.GetFullPath(Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(audio)}.txt"));
            EnsureContained(transcriptPath, outputDirectory);
            if (!File.Exists(transcriptPath))
            {
                throw new VoiceOperationException("Whisper did not produce a transcript.");
            }

            var transcript = (await File.ReadAllTextAsync(
                    transcriptPath,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false)).Trim();
            if (transcript.Length == 0)
            {
                throw new VoiceOperationException("Whisper produced an empty transcript.");
            }

            return transcript;
        }
        finally
        {
            CleanupOutputDirectory(outputDirectory);
        }
    }

    private static string RequireCompleteFile(string? path, string error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new VoiceOperationException(error);
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                throw new VoiceOperationException(error);
            }

            return fullPath;
        }
        catch (VoiceOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or UnauthorizedAccessException)
        {
            throw new VoiceOperationException(error, exception);
        }
    }

    private static void EnsureContained(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new VoiceOperationException("Transcript path escaped its output directory.");
        }
    }

    private void CleanupOutputDirectory(string outputDirectory)
    {
        var normalizedRoot = temporaryRoot
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedOutput = Path.GetFullPath(outputDirectory);
        if (!normalizedOutput.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new VoiceOperationException("Refusing to clean a directory outside the temporary root.");
        }

        if (Directory.Exists(normalizedOutput))
        {
            Directory.Delete(normalizedOutput, recursive: true);
        }
    }
}
