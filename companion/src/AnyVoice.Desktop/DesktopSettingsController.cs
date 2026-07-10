using AnyVoice.Core;

namespace AnyVoice.Desktop;

public sealed record DesktopBounds(double Left, double Top, double Width, double Height);

public sealed record DesktopPresentation(
    double Scale,
    double Opacity,
    bool ShowSubtitle,
    bool AutoHideSubtitle,
    int SubtitleDurationSeconds,
    double? WindowLeft,
    double? WindowTop);

public sealed class DesktopSettingsController
{
    private const double BaseWindowWidth = 240;
    private const double BaseWindowHeight = 330;
    private const double MinimumVisiblePixels = 40;

    private CompanionSettings settings;
    private DesktopDisplayState displayState;

    public DesktopSettingsController(
        CompanionSettings settings,
        DesktopDisplayState displayState)
    {
        this.settings = settings.Normalize();
        this.displayState = displayState;
    }

    public event EventHandler? Changed;

    public CompanionSettings Settings => settings;

    public DesktopDisplayState DisplayState => displayState;

    public void UpdateSettings(CompanionSettings value)
    {
        settings = value.Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateDisplayState(DesktopDisplayState value)
    {
        displayState = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public DesktopPresentation BuildPresentation(DesktopBounds bounds)
    {
        var (left, top) = ResolvePlacement(bounds);
        var showSubtitle = settings.SubtitlesEnabled
            && !string.IsNullOrWhiteSpace(displayState.Subtitle);
        return new DesktopPresentation(
            settings.Scale,
            settings.Opacity,
            showSubtitle,
            showSubtitle && !displayState.IsPersistent,
            settings.SubtitleDurationSeconds,
            left,
            top);
    }

    private (double? Left, double? Top) ResolvePlacement(DesktopBounds bounds)
    {
        if (settings.WindowLeft is not { } left || settings.WindowTop is not { } top)
        {
            return (null, null);
        }

        var width = BaseWindowWidth * settings.Scale;
        var height = BaseWindowHeight * settings.Scale;
        var intersects = left + width >= bounds.Left + MinimumVisiblePixels
            && left <= bounds.Left + bounds.Width - MinimumVisiblePixels
            && top + height >= bounds.Top + MinimumVisiblePixels
            && top <= bounds.Top + bounds.Height - MinimumVisiblePixels;
        return intersects ? (left, top) : (null, null);
    }
}
