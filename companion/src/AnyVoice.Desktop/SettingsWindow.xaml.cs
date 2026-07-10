using System.Windows;
using AnyVoice.Core;
using AnyVoice.Core.Voice;

namespace AnyVoice.Desktop;

public partial class SettingsWindow : Window
{
    private readonly CompanionSettings original;
    private readonly Func<CompanionSettings, CancellationToken, Task<VoiceStatus>> statusProvider;

    public SettingsWindow(
        CompanionSettings settings,
        Func<CompanionSettings, CancellationToken, Task<VoiceStatus>> statusProvider)
    {
        InitializeComponent();
        original = settings.Normalize();
        this.statusProvider = statusProvider
            ?? throw new ArgumentNullException(nameof(statusProvider));
        ScaleSlider.Value = original.Scale * 100;
        OpacitySlider.Value = original.Opacity * 100;
        SubtitleDurationSlider.Value = original.SubtitleDurationSeconds;
        SubtitlesCheckBox.IsChecked = original.SubtitlesEnabled;
        SpeechCheckBox.IsChecked = original.SpeechEnabled;
        HotkeyCheckBox.IsChecked = original.HotkeyEnabled;
        FfmpegPathTextBox.Text = original.FfmpegPath ?? string.Empty;
        WhisperPathTextBox.Text = original.WhisperPath ?? string.Empty;
        ModelPathTextBox.Text = original.WhisperModelPath ?? string.Empty;
        AudioDeviceTextBox.Text = original.AudioDevice ?? string.Empty;

        ScaleSlider.ValueChanged += (_, _) => UpdateValueLabels();
        OpacitySlider.ValueChanged += (_, _) => UpdateValueLabels();
        SubtitleDurationSlider.ValueChanged += (_, _) => UpdateValueLabels();
        UpdateValueLabels();
    }

    public CompanionSettings? ResultSettings { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ResultSettings = (original with
        {
            Scale = ScaleSlider.Value / 100,
            Opacity = OpacitySlider.Value / 100,
            SubtitleDurationSeconds = (int)SubtitleDurationSlider.Value,
            SubtitlesEnabled = SubtitlesCheckBox.IsChecked == true,
            SpeechEnabled = SpeechCheckBox.IsChecked == true,
            HotkeyEnabled = HotkeyCheckBox.IsChecked == true,
            FfmpegPath = FfmpegPathTextBox.Text,
            WhisperPath = WhisperPathTextBox.Text,
            WhisperModelPath = ModelPathTextBox.Text,
            AudioDevice = AudioDeviceTextBox.Text,
        }).Normalize();
        DialogResult = true;
    }

    private void UpdateValueLabels()
    {
        ScaleValue.Text = $"{ScaleSlider.Value:0}%";
        OpacityValue.Text = $"{OpacitySlider.Value:0}%";
        SubtitleDurationValue.Text = $"{SubtitleDurationSlider.Value:0} sec";
    }

    private async void DetectVoice_Click(object sender, RoutedEventArgs e)
    {
        DetectVoiceButton.IsEnabled = false;
        VoiceStatusText.Text = "Checking local tools...";
        try
        {
            var candidate = (original with
            {
                FfmpegPath = FfmpegPathTextBox.Text,
                WhisperPath = WhisperPathTextBox.Text,
                WhisperModelPath = ModelPathTextBox.Text,
                AudioDevice = AudioDeviceTextBox.Text,
            }).Normalize();
            var status = await statusProvider(candidate, CancellationToken.None);
            FfmpegPathTextBox.Text = status.Tools.FfmpegPath ?? string.Empty;
            WhisperPathTextBox.Text = status.Tools.WhisperPath ?? string.Empty;
            ModelPathTextBox.Text = status.Tools.ModelPath ?? string.Empty;
            if (status.Microphones.Count > 0
                && string.IsNullOrWhiteSpace(AudioDeviceTextBox.Text))
            {
                AudioDeviceTextBox.Text = status.Microphones[0];
            }

            VoiceStatusText.Text = status.IsReady
                ? "Voice tools are ready."
                : string.Join(" ", status.Errors);
        }
        catch (OperationCanceledException)
        {
            VoiceStatusText.Text = "Local tool check was cancelled.";
        }
        finally
        {
            DetectVoiceButton.IsEnabled = true;
        }
    }
}
