namespace AnyVoice.Core.Voice;

public sealed record VoiceToolPaths(
    string? FfmpegPath,
    string? WhisperPath,
    string? ModelPath);
