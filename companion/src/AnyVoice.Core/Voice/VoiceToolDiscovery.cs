namespace AnyVoice.Core.Voice;

public sealed class VoiceToolDiscovery
{
    private static readonly string[] ModelPreference =
    [
        "tiny",
        "base",
        "small",
        "medium",
        "large",
        "turbo",
    ];

    private readonly string pathVariable;
    private readonly string userProfile;

    public VoiceToolDiscovery(string? pathVariable = null, string? userProfile = null)
    {
        this.pathVariable = pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        this.userProfile = Path.GetFullPath(
            userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public VoiceToolPaths Discover(CompanionSettings settings)
    {
        var normalized = settings.Normalize();
        return new VoiceToolPaths(
            ResolveExecutable(normalized.FfmpegPath, "ffmpeg.exe"),
            ResolveExecutable(normalized.WhisperPath, "whisper.exe"),
            ResolveModel(normalized.WhisperModelPath));
    }

    private string? ResolveExecutable(string? explicitPath, string fileName)
    {
        var explicitValue = ResolveCompleteFile(explicitPath);
        if (explicitValue is not null)
        {
            return explicitValue;
        }

        foreach (var rawDirectory in pathVariable.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(rawDirectory.Trim('"'), fileName));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        return null;
    }

    private string? ResolveModel(string? explicitPath)
    {
        var explicitValue = ResolveCompleteFile(explicitPath);
        if (explicitValue is not null)
        {
            return explicitValue;
        }

        var cache = Path.Combine(userProfile, ".cache", "whisper");
        if (!Directory.Exists(cache))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(cache, "*.pt", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(file => file.Length > 0)
                .OrderBy(file => GetModelRank(Path.GetFileNameWithoutExtension(file.Name)))
                .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ResolveCompleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            return File.Exists(fullPath) && new FileInfo(fullPath).Length > 0 ? fullPath : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or PathTooLongException
                or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int GetModelRank(string name)
    {
        for (var index = 0; index < ModelPreference.Length; index++)
        {
            if (name.StartsWith(ModelPreference[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return ModelPreference.Length;
    }
}
