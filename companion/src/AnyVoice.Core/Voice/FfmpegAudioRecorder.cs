using System.Diagnostics;

namespace AnyVoice.Core.Voice;

public sealed class FfmpegAudioRecorder : IAudioRecorder
{
    private readonly object sync = new();
    private Process? process;
    private Task<string>? stderrTask;
    private string? outputPath;

    public async Task StartAsync(
        string ffmpegPath,
        string microphone,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(microphone);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var executable = Path.GetFullPath(ffmpegPath);
        if (!File.Exists(executable))
        {
            throw new VoiceOperationException("FFmpeg executable is missing.");
        }

        var recording = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(recording)!);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-f",
            "dshow",
            "-i",
            $"audio={microphone}",
            "-ac",
            "1",
            "-ar",
            "16000",
            "-c:a",
            "pcm_s16le",
            recording,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        var candidate = new Process { StartInfo = startInfo };
        lock (sync)
        {
            if (process is not null)
            {
                candidate.Dispose();
                throw new VoiceOperationException("A recording is already active.");
            }

            process = candidate;
            this.outputPath = recording;
        }

        try
        {
            if (!candidate.Start())
            {
                throw new VoiceOperationException("FFmpeg could not be started.");
            }

            stderrTask = candidate.StandardError.ReadToEndAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken)
                .ConfigureAwait(false);
            if (candidate.HasExited)
            {
                await ResetProcessAsync(candidate).ConfigureAwait(false);
                throw new VoiceOperationException("FFmpeg stopped before recording began.");
            }
        }
        catch
        {
            Kill(candidate);
            await ResetProcessAsync(candidate).ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process active;
        lock (sync)
        {
            active = process
                ?? throw new VoiceOperationException("No recording is active.");
        }

        try
        {
            if (!active.HasExited)
            {
                await active.StandardInput.WriteLineAsync("q".AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await active.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await active.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new VoiceOperationException("FFmpeg did not stop in time.");
                }
            }

            if (active.ExitCode != 0)
            {
                throw new VoiceOperationException("FFmpeg recording failed.");
            }

            var recording = outputPath;
            if (string.IsNullOrWhiteSpace(recording)
                || !File.Exists(recording)
                || new FileInfo(recording).Length == 0)
            {
                throw new VoiceOperationException("FFmpeg did not produce an audio file.");
            }
        }
        catch
        {
            Kill(active);
            throw;
        }
        finally
        {
            await ResetProcessAsync(active).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Process? active;
        lock (sync)
        {
            active = process;
        }

        if (active is null)
        {
            return;
        }

        Kill(active);
        await ResetProcessAsync(active).ConfigureAwait(false);
    }

    private async Task ResetProcessAsync(Process active)
    {
        Task<string>? errorOutput;
        lock (sync)
        {
            if (!ReferenceEquals(process, active))
            {
                return;
            }

            process = null;
            outputPath = null;
            errorOutput = stderrTask;
            stderrTask = null;
        }

        if (errorOutput is not null)
        {
            try
            {
                await errorOutput.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The process is already stopped; cancellation only ended stream draining.
            }
        }

        active.Dispose();
    }

    private static void Kill(Process active)
    {
        try
        {
            if (!active.HasExited)
            {
                active.Kill(entireProcessTree: true);
                active.WaitForExit(2000);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            // The process may have exited between the state check and the kill request.
        }
    }
}
