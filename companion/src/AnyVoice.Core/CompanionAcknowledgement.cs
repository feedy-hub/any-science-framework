namespace AnyVoice.Core;

public sealed record CompanionAcknowledgement(bool Accepted, string? Error)
{
    public static CompanionAcknowledgement Success { get; } = new(true, null);

    public static CompanionAcknowledgement Unavailable { get; } = new(false, "companion-unavailable");
}
