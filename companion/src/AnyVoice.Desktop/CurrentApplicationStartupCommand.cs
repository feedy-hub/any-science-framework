using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using AnyVoice.Core.Startup;

namespace AnyVoice.Desktop;

public static class CurrentApplicationStartupCommand
{
    public static string Build()
    {
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        var processPath = Environment.ProcessPath;
        var dotnetPath = string.Equals(
            Path.GetFileName(processPath),
            "dotnet.exe",
            StringComparison.OrdinalIgnoreCase)
            ? processPath
            : ResolveDotnetFromRuntime();
        return StartupLaunchCommand.Build(
            dotnetPath ?? string.Empty,
            assemblyPath ?? string.Empty);
    }

    private static string ResolveDotnetFromRuntime()
    {
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot = runtime.Parent?.Parent?.Parent
            ?? throw new StartupRegistrationException("The .NET host is unavailable.");
        return Path.Combine(dotnetRoot.FullName, "dotnet.exe");
    }
}
