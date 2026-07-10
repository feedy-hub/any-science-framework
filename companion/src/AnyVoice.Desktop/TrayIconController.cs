using System.Drawing;
using AnyVoice.Core.Voice;
using Forms = System.Windows.Forms;

namespace AnyVoice.Desktop;

public sealed class TrayIconController : IDisposable
{
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Forms.ToolStripMenuItem visibilityItem;
    private readonly Forms.ToolStripMenuItem subtitlesItem;
    private readonly Forms.ToolStripMenuItem speechItem;
    private readonly Forms.ToolStripMenuItem dictationItem;
    private bool updating;

    public TrayIconController()
    {
        visibilityItem = new Forms.ToolStripMenuItem("Hide character");
        visibilityItem.Click += (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);

        subtitlesItem = new Forms.ToolStripMenuItem("Subtitles") { CheckOnClick = true };
        subtitlesItem.CheckedChanged += (_, _) =>
        {
            if (!updating)
            {
                SubtitlesChanged?.Invoke(this, subtitlesItem.Checked);
            }
        };

        speechItem = new Forms.ToolStripMenuItem("Speech") { CheckOnClick = true };
        speechItem.CheckedChanged += (_, _) =>
        {
            if (!updating)
            {
                SpeechChanged?.Invoke(this, speechItem.Checked);
            }
        };

        dictationItem = new Forms.ToolStripMenuItem("Start dictation");
        dictationItem.Click += (_, _) => DictationRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.AddRange(
        new Forms.ToolStripItem[]
        {
            visibilityItem,
            subtitlesItem,
            speechItem,
            new Forms.ToolStripSeparator(),
            dictationItem,
            settingsItem,
            new Forms.ToolStripSeparator(),
            exitItem,
        });

        notifyIcon = new Forms.NotifyIcon
        {
            Text = "AnyVoice Companion",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ToggleVisibilityRequested;

    public event EventHandler<bool>? SubtitlesChanged;

    public event EventHandler<bool>? SpeechChanged;

    public event EventHandler? SettingsRequested;

    public event EventHandler? DictationRequested;

    public event EventHandler? ExitRequested;

    public void Update(
        bool characterVisible,
        bool subtitlesEnabled,
        bool speechEnabled,
        DictationState dictationState)
    {
        updating = true;
        try
        {
            visibilityItem.Text = characterVisible ? "Hide character" : "Show character";
            subtitlesItem.Checked = subtitlesEnabled;
            speechItem.Checked = speechEnabled;
            dictationItem.Text = dictationState == DictationState.Recording
                ? "Stop and transcribe"
                : dictationState == DictationState.Transcribing
                    ? "Transcribing..."
                    : "Start dictation";
            dictationItem.Enabled = dictationState != DictationState.Transcribing;
        }
        finally
        {
            updating = false;
        }
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

}
