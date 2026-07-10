namespace AnyVoice.Protocol;

public sealed record CompanionEvent(
    int SchemaVersion,
    CompanionEventType Type,
    string Source,
    string? Text,
    DateTimeOffset CreatedAtUtc)
{
    public static CompanionEvent Create(
        CompanionEventType type,
        string source,
        string? text = null)
    {
        return new CompanionEvent(1, type, source, text, DateTimeOffset.UtcNow);
    }
}
