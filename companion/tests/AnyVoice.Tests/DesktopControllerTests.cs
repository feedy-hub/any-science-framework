using AnyVoice.Desktop;
using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class DesktopControllerTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("desktop controller maps supported states", () =>
        {
            var controller = new DesktopEventController();
            var expected = new[]
            {
                CompanionEventType.Thinking,
                CompanionEventType.Speaking,
                CompanionEventType.Success,
                CompanionEventType.NeedsInput,
                CompanionEventType.Error,
            };

            foreach (var eventType in expected)
            {
                controller.Apply(CompanionEvent.Create(eventType, "manual", eventType.ToString()));
                suite.Equal(eventType, controller.Current.Type);
            }
        });

        suite.Test("desktop controller sanitizes subtitle and marks persistent states", () =>
        {
            var controller = new DesktopEventController();
            DesktopDisplayState? observed = null;
            controller.StateChanged += (_, state) => observed = state;

            controller.Apply(CompanionEvent.Create(
                CompanionEventType.NeedsInput,
                "codex",
                @"Check C:\Users\PS\private.txt token=abc123"));

            suite.Equal("Check [path] token=[redacted]", controller.Current.Subtitle);
            suite.Equal(true, controller.Current.IsPersistent);
            suite.Equal(controller.Current, observed);
        });

        suite.Test("desktop controller supplies safe fallback text", () =>
        {
            var controller = new DesktopEventController();

            controller.Apply(CompanionEvent.Create(CompanionEventType.Success, "claude"));

            suite.Equal("已完成。", controller.Current.Subtitle);
            suite.Equal(false, controller.Current.IsPersistent);
        });
    }
}
