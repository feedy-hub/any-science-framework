using AnyVoice.Protocol;

namespace AnyVoice.Desktop;

public sealed class DesktopEventController
{
    public DesktopEventController()
    {
        Current = BuildState(CompanionEvent.Create(CompanionEventType.Idle, "manual"));
    }

    public event EventHandler<DesktopDisplayState>? StateChanged;

    public DesktopDisplayState Current { get; private set; }

    public void Apply(CompanionEvent value)
    {
        CompanionEventValidator.Validate(value);
        Current = BuildState(value);
        StateChanged?.Invoke(this, Current);
    }

    private static DesktopDisplayState BuildState(CompanionEvent value)
    {
        var subtitle = SpeechTextSanitizer.Sanitize(value.Text);
        if (string.IsNullOrEmpty(subtitle))
        {
            subtitle = GetFallback(value.Type);
        }

        var isPersistent = value.Type is CompanionEventType.NeedsInput or CompanionEventType.Error;
        return new DesktopDisplayState(value.Type, subtitle, isPersistent);
    }

    private static string GetFallback(CompanionEventType type)
    {
        return type switch
        {
            CompanionEventType.Idle => "Ready.",
            CompanionEventType.Listening => "Listening.",
            CompanionEventType.Thinking => "Working.",
            CompanionEventType.Speaking => "Speaking.",
            CompanionEventType.Success => "Completed.",
            CompanionEventType.NeedsInput => "Input needed.",
            CompanionEventType.Error => "Something needs attention.",
            _ => "Ready.",
        };
    }
}
