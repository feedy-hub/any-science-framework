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
            CompanionEventType.Idle => "就绪。",
            CompanionEventType.Listening => "正在聆听。",
            CompanionEventType.Thinking => "正在处理。",
            CompanionEventType.Speaking => "正在播报。",
            CompanionEventType.Success => "已完成。",
            CompanionEventType.NeedsInput => "需要输入。",
            CompanionEventType.Error => "出现需要处理的问题。",
            _ => "就绪。",
        };
    }
}
