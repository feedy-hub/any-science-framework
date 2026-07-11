namespace AnyVoice.Core.Startup;

public static class StartupLaunchCommand
{
    public static string Build(string dotnetPath, string assemblyPath)
    {
        var dotnet = RequireAbsoluteFile(dotnetPath, "The .NET host is unavailable.");
        var assembly = RequireAbsoluteFile(assemblyPath, "The companion assembly is unavailable.");
        return $"\"{dotnet}\" \"{assembly}\"";
    }

    private static string RequireAbsoluteFile(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new StartupRegistrationException(message);
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                throw new StartupRegistrationException(message);
            }

            return fullPath;
        }
        catch (StartupRegistrationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or UnauthorizedAccessException)
        {
            throw new StartupRegistrationException(message, exception);
        }
    }
}
