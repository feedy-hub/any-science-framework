namespace AnyVoice.Core;

public sealed class CompanionPaths
{
    public CompanionPaths(string localApplicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataRoot);
        var root = Path.GetFullPath(localApplicationDataRoot);
        BaseDirectory = Path.Combine(root, "AnyVoiceCompanion");
        ConfigFile = Path.Combine(BaseDirectory, "config.json");
        CharactersDirectory = Path.Combine(BaseDirectory, "characters");
        LogsDirectory = Path.Combine(BaseDirectory, "logs");
    }

    public string BaseDirectory { get; }

    public string ConfigFile { get; }

    public string CharactersDirectory { get; }

    public string LogsDirectory { get; }

    public static CompanionPaths ForCurrentUser()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        return new CompanionPaths(root);
    }
}
