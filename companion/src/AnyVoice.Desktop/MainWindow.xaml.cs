using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AnyVoice.Protocol;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace AnyVoice.Desktop;

public partial class MainWindow : Window
{
    private static readonly Brush IdleBrush = FrozenBrush("#42B6A5");
    private static readonly Brush ListeningBrush = FrozenBrush("#3D8DCE");
    private static readonly Brush ThinkingBrush = FrozenBrush("#D6A33D");
    private static readonly Brush SpeakingBrush = FrozenBrush("#8E6CC7");
    private static readonly Brush SuccessBrush = FrozenBrush("#35A76F");
    private static readonly Brush AttentionBrush = FrozenBrush("#E75E65");

    private readonly DesktopSettingsController settingsController;
    private readonly DispatcherTimer subtitleTimer;

    public MainWindow(DesktopSettingsController settingsController)
    {
        InitializeComponent();
        this.settingsController = settingsController;
        subtitleTimer = new DispatcherTimer();
        subtitleTimer.Tick += (_, _) =>
        {
            subtitleTimer.Stop();
            SubtitleBubble.Visibility = Visibility.Collapsed;
        };
        settingsController.Changed += (_, _) => ApplyPresentation(applyPlacement: false);
        ApplyPresentation(applyPlacement: true);
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? DictationRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<WindowPlacementChangedEventArgs>? PlacementChanged;

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ApplyPresentation(bool applyPlacement)
    {
        var bounds = new DesktopBounds(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var presentation = settingsController.BuildPresentation(bounds);
        var state = settingsController.DisplayState;
        Width = 240 * presentation.Scale;
        Height = 330 * presentation.Scale;
        Opacity = presentation.Opacity;
        SubtitleBubble.Visibility = presentation.ShowSubtitle
            ? Visibility.Visible
            : Visibility.Collapsed;
        SubtitleText.Text = state.Subtitle;
        StatusText.Text = GetStatusLabel(state.Type);
        var brush = GetStateBrush(state.Type);
        CharacterFace.Fill = brush;
        StatusHalo.Fill = WithOpacity(brush, 0.2);
        CharacterMouth.Height = state.Type == CompanionEventType.Speaking ? 18 : 6;
        CharacterMouth.Width = state.Type == CompanionEventType.Speaking ? 18 : 28;

        subtitleTimer.Stop();
        if (presentation.AutoHideSubtitle)
        {
            subtitleTimer.Interval = TimeSpan.FromSeconds(presentation.SubtitleDurationSeconds);
            subtitleTimer.Start();
        }

        if (applyPlacement && presentation.WindowLeft is { } left && presentation.WindowTop is { } top)
        {
            Left = left;
            Top = top;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            PlacementChanged?.Invoke(this, new WindowPlacementChangedEventArgs(Left, Top));
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Dictation_Click(object sender, RoutedEventArgs e)
    {
        DictationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string GetStatusLabel(CompanionEventType type)
    {
        return type switch
        {
            CompanionEventType.NeedsInput => "NEEDS INPUT",
            _ => type.ToString().ToUpperInvariant(),
        };
    }

    private static Brush GetStateBrush(CompanionEventType type)
    {
        return type switch
        {
            CompanionEventType.Listening => ListeningBrush,
            CompanionEventType.Thinking => ThinkingBrush,
            CompanionEventType.Speaking => SpeakingBrush,
            CompanionEventType.Success => SuccessBrush,
            CompanionEventType.NeedsInput or CompanionEventType.Error => AttentionBrush,
            _ => IdleBrush,
        };
    }

    private static SolidColorBrush FrozenBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush WithOpacity(Brush brush, double opacity)
    {
        var color = ((SolidColorBrush)brush).Color;
        var value = new SolidColorBrush(color) { Opacity = opacity };
        value.Freeze();
        return value;
    }
}

public sealed class WindowPlacementChangedEventArgs(double left, double top) : EventArgs
{
    public double Left { get; } = left;

    public double Top { get; } = top;
}
