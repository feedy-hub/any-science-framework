namespace AnyVoice.Core.Voice;

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? StandardInput,
    IReadOnlyDictionary<string, string?> Environment,
    TimeSpan Timeout,
    int MaximumCapturedCharacters = 65_536);
