using AnyVoice.Protocol;

namespace AnyVoice.Desktop;

public sealed record DesktopDisplayState(
    CompanionEventType Type,
    string Subtitle,
    bool IsPersistent);
