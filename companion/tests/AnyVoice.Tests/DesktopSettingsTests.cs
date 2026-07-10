using AnyVoice.Core;
using AnyVoice.Desktop;
using AnyVoice.Protocol;

namespace AnyVoice.Tests;

internal static class DesktopSettingsTests
{
    public static void Register(TestSuite suite)
    {
        suite.Test("presentation uses normalized settings", () =>
        {
            var controller = new DesktopSettingsController(
                new CompanionSettings { Scale = 9, Opacity = 0.1, SubtitleDurationSeconds = 90 },
                new DesktopDisplayState(CompanionEventType.Idle, "Ready.", false));

            var value = controller.BuildPresentation(new DesktopBounds(0, 0, 1920, 1080));

            suite.Equal(2.0, value.Scale);
            suite.Equal(0.35, value.Opacity);
            suite.Equal(30, value.SubtitleDurationSeconds);
        });

        suite.Test("off-screen placement is discarded", () =>
        {
            var controller = new DesktopSettingsController(
                CompanionSettings.Default with { WindowLeft = 10_000, WindowTop = -10_000 },
                new DesktopDisplayState(CompanionEventType.Idle, "Ready.", false));

            var value = controller.BuildPresentation(new DesktopBounds(0, 0, 1920, 1080));

            suite.Equal<double?>(null, value.WindowLeft);
            suite.Equal<double?>(null, value.WindowTop);
        });

        suite.Test("subtitle setting and persistence control auto hide", () =>
        {
            var controller = new DesktopSettingsController(
                CompanionSettings.Default,
                new DesktopDisplayState(CompanionEventType.Success, "Done.", false));

            var transient = controller.BuildPresentation(new DesktopBounds(0, 0, 1920, 1080));
            suite.Equal(true, transient.ShowSubtitle);
            suite.Equal(true, transient.AutoHideSubtitle);

            controller.UpdateDisplayState(
                new DesktopDisplayState(CompanionEventType.NeedsInput, "Choose one.", true));
            var persistent = controller.BuildPresentation(new DesktopBounds(0, 0, 1920, 1080));
            suite.Equal(true, persistent.ShowSubtitle);
            suite.Equal(false, persistent.AutoHideSubtitle);

            controller.UpdateSettings(CompanionSettings.Default with { SubtitlesEnabled = false });
            var hidden = controller.BuildPresentation(new DesktopBounds(0, 0, 1920, 1080));
            suite.Equal(false, hidden.ShowSubtitle);
            suite.Equal(false, hidden.AutoHideSubtitle);
        });
    }
}
