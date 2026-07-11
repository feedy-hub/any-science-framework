using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using AnyVoice.Core;
using AnyVoice.Core.Startup;
using AnyVoice.Core.Voice;
using AnyVoice.Protocol;

namespace AnyVoice.Desktop;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource shutdown = new();
    private SingleInstanceCoordinator? singleInstance;
    private CompanionPipeServer? pipeServer;
    private Task? pipeServerTask;
    private CompanionSettingsStore? settingsStore;
    private CompanionSettings settings = CompanionSettings.Default;
    private DesktopEventController? eventController;
    private DesktopSettingsController? settingsController;
    private MainWindow? companionWindow;
    private TrayIconController? trayIcon;
    private GlobalHotkeyController? globalHotkey;
    private VoiceStatusService? voiceStatusService;
    private DictationController? dictationController;
    private FfmpegAudioRecorder? audioRecorder;
    private SpeechCoordinator? speechCoordinator;
    private StartupRegistrationService? startupRegistration;
    private bool explicitShutdown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstance = new SingleInstanceCoordinator(
            SingleInstanceCoordinator.GetCurrentUserName());
        if (!singleInstance.OwnsInstance)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        var paths = CompanionPaths.ForCurrentUser();
        settingsStore = new CompanionSettingsStore(paths.ConfigFile);
        try
        {
            settings = settingsStore.Load();
        }
        catch (IOException)
        {
            settings = CompanionSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            settings = CompanionSettings.Default;
        }

        var processRunner = new ProcessRunner();
        var voiceDiscovery = new VoiceToolDiscovery();
        voiceStatusService = new VoiceStatusService(voiceDiscovery, processRunner);
        audioRecorder = new FfmpegAudioRecorder();
        dictationController = new DictationController(
            audioRecorder,
            new WhisperTranscriber(processRunner, Path.Combine(paths.BaseDirectory, "temp")),
            () => voiceDiscovery.Discover(settings),
            () => settings.AudioDevice,
            Path.Combine(paths.BaseDirectory, "temp"),
            settings.RetainDiagnosticAudio);
        speechCoordinator = new SpeechCoordinator(
            new PowerShellSpeechOutput(processRunner),
            () => settings.SpeechEnabled);

        eventController = new DesktopEventController();
        settingsController = new DesktopSettingsController(settings, eventController.Current);
        companionWindow = new MainWindow(settingsController);
        MainWindow = companionWindow;
        eventController.StateChanged += (_, state) =>
            _ = Dispatcher.BeginInvoke(() => settingsController.UpdateDisplayState(state));
        companionWindow.SettingsRequested += (_, _) => OpenSettings();
        companionWindow.DictationRequested += (_, _) => _ = ToggleDictationAsync();
        companionWindow.ExitRequested += (_, _) => ExitApplication();
        companionWindow.PlacementChanged += (_, placement) => SavePlacement(placement);
        companionWindow.Closing += (_, args) =>
        {
            if (!explicitShutdown)
            {
                args.Cancel = true;
                companionWindow.Hide();
                UpdateTray();
            }
        };

        dictationController.StateEvent += (_, value) =>
            _ = Dispatcher.BeginInvoke(() =>
            {
                ApplyDesktopEvent(value, allowSpeech: false);
                UpdateTray();
            });
        dictationController.TranscriptReady += (_, transcript) =>
            CopyTranscriptToClipboard(transcript);

        trayIcon = new TrayIconController();
        trayIcon.ToggleVisibilityRequested += (_, _) =>
            _ = Dispatcher.BeginInvoke(ToggleCharacterVisibility);
        trayIcon.SubtitlesChanged += (_, enabled) =>
            _ = Dispatcher.BeginInvoke(() => ApplySettings(settings with { SubtitlesEnabled = enabled }));
        trayIcon.SpeechChanged += (_, enabled) =>
            _ = Dispatcher.BeginInvoke(() => ApplySettings(settings with { SpeechEnabled = enabled }));
        trayIcon.DictationRequested += (_, _) =>
            _ = Dispatcher.BeginInvoke(() => _ = ToggleDictationAsync());
        trayIcon.SettingsRequested += (_, _) => _ = Dispatcher.BeginInvoke(OpenSettings);
        trayIcon.ExitRequested += (_, _) => _ = Dispatcher.BeginInvoke(ExitApplication);

        globalHotkey = new GlobalHotkeyController(companionWindow);
        globalHotkey.Triggered += (_, _) => _ = ToggleDictationAsync();
        globalHotkey.RegistrationFailed += (_, _) => HandleHotkeyRegistrationFailed();
        globalHotkey.UpdateEnabled(settings.HotkeyEnabled);
        InitializeStartupRegistration();

        pipeServer = new CompanionPipeServer(
            CompanionPipeNames.ForCurrentUser(),
            (value, cancellationToken) =>
            {
                _ = Dispatcher.BeginInvoke(() =>
                {
                    if (value.Source == "desktop-activation")
                    {
                        ShowCharacter();
                    }

                    ApplyDesktopEvent(value, allowSpeech: true);
                });
                return Task.CompletedTask;
            });

        companionWindow.Show();
        UpdateTray();
        pipeServerTask = RunServerAsync(shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        explicitShutdown = true;
        globalHotkey?.Dispose();
        trayIcon?.Dispose();
        shutdown.Cancel();
        if (audioRecorder is not null)
        {
            try
            {
                audioRecorder.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }
        }

        try
        {
            _ = pipeServerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception) when (
            exception.InnerExceptions.All(item => item is OperationCanceledException))
        {
        }

        singleInstance?.Dispose();
        shutdown.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        explicitShutdown = true;
        base.OnSessionEnding(e);
    }

    private static void ActivateExistingInstance()
    {
        try
        {
            var client = new CompanionPipeClient(
                CompanionPipeNames.ForCurrentUser(),
                TimeSpan.FromSeconds(2));
            _ = client.SendAsync(
                    CompanionEvent.Create(
                        CompanionEventType.Idle,
                        "desktop-activation",
                        "就绪。"))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception) when (
            exception is CompanionProtocolException
                or ArgumentException
                or IOException
                or TimeoutException)
        {
        }
    }

    private void ToggleCharacterVisibility()
    {
        if (companionWindow?.IsVisible == true)
        {
            companionWindow.Hide();
        }
        else
        {
            ShowCharacter();
        }

        UpdateTray();
    }

    private void ExitApplication()
    {
        explicitShutdown = true;
        Shutdown();
    }

    private void HandleHotkeyRegistrationFailed()
    {
        if (settings.HotkeyEnabled)
        {
            ApplySettings(settings with { HotkeyEnabled = false });
        }

        ApplyDesktopEvent(
            CompanionEvent.Create(
                CompanionEventType.Error,
                "desktop",
                "Ctrl+Alt+V 已被占用，听写热键已关闭。"),
            allowSpeech: false);
    }

    private void ShowCharacter()
    {
        companionWindow?.ShowAndActivate();
        UpdateTray();
    }

    private void OpenSettings()
    {
        if (companionWindow is null || voiceStatusService is null)
        {
            return;
        }

        var window = new SettingsWindow(settings, voiceStatusService.InspectAsync)
        {
            Owner = companionWindow,
        };
        if (window.ShowDialog() == true && window.ResultSettings is { } updated)
        {
            ApplySettings(updated);
        }
    }

    private void ApplySettings(CompanionSettings value)
    {
        var normalized = value.Normalize();
        var previous = settings;
        var startupChanged = normalized.StartWithWindows != previous.StartWithWindows;
        try
        {
            if (startupChanged)
            {
                GetStartupRegistration().SetEnabled(normalized.StartWithWindows);
            }

            settingsStore?.Save(normalized);
            settings = normalized;
            settingsController?.UpdateSettings(settings);
            globalHotkey?.UpdateEnabled(settings.HotkeyEnabled);
            UpdateTray();
        }
        catch (StartupRegistrationException)
        {
            System.Windows.MessageBox.Show(
                "无法更新 Windows 开机自启动设置，原设置已保留。",
                "AnyVoice 设置",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (startupChanged)
            {
                TryRestoreStartupRegistration(previous.StartWithWindows);
            }

            System.Windows.MessageBox.Show(
                "AnyVoice 无法保存设置。",
                "AnyVoice 设置",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void InitializeStartupRegistration()
    {
        try
        {
            startupRegistration = new StartupRegistrationService(
                new WindowsStartupValueStore(),
                CurrentApplicationStartupCommand.Build());
            startupRegistration.SetEnabled(settings.StartWithWindows);
        }
        catch (StartupRegistrationException)
        {
            startupRegistration = null;
            if (settings.StartWithWindows)
            {
                ApplyDesktopEvent(
                    CompanionEvent.Create(
                        CompanionEventType.Error,
                        "desktop",
                        "开机自启动注册失败，请在设置中重新启用。"),
                    allowSpeech: false);
            }
        }
    }

    private StartupRegistrationService GetStartupRegistration()
    {
        return startupRegistration
            ?? throw new StartupRegistrationException("Windows startup registration is unavailable.");
    }

    private void TryRestoreStartupRegistration(bool enabled)
    {
        try
        {
            startupRegistration?.SetEnabled(enabled);
        }
        catch (StartupRegistrationException)
        {
            // The primary settings error is shown to the user; rollback remains best-effort.
        }
    }

    private void SavePlacement(WindowPlacementChangedEventArgs placement)
    {
        ApplySettings(settings with
        {
            WindowLeft = placement.Left,
            WindowTop = placement.Top,
        });
    }

    private void UpdateTray()
    {
        trayIcon?.Update(
            companionWindow?.IsVisible == true,
            settings.SubtitlesEnabled,
            settings.SpeechEnabled,
            dictationController?.State ?? DictationState.Idle);
    }

    private async Task ToggleDictationAsync()
    {
        if (dictationController is null)
        {
            return;
        }

        try
        {
            await dictationController.ToggleAsync(shutdown.Token);
        }
        catch (VoiceOperationException)
        {
            ApplyDesktopEvent(
                CompanionEvent.Create(
                    CompanionEventType.Error,
                    "dictation",
                    "听写正在处理中。"),
                allowSpeech: false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            UpdateTray();
        }
    }

    private void CopyTranscriptToClipboard(string transcript)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                System.Windows.Clipboard.SetText(transcript);
            }
            else
            {
                Dispatcher.Invoke(() => System.Windows.Clipboard.SetText(transcript));
            }
        }
        catch (ExternalException exception)
        {
            throw new VoiceOperationException("The transcript could not be copied.", exception);
        }
    }

    private void ApplyDesktopEvent(CompanionEvent value, bool allowSpeech)
    {
        eventController?.Apply(value);
        if (allowSpeech && speechCoordinator is not null)
        {
            _ = SpeakSafelyAsync(value);
        }
    }

    private async Task SpeakSafelyAsync(CompanionEvent value)
    {
        try
        {
            await speechCoordinator!.NotifyAsync(value, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await pipeServer!.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                var value = CompanionEvent.Create(
                    CompanionEventType.Error,
                    "desktop",
                    "本地伴侣连接不可用。");
                _ = Dispatcher.BeginInvoke(() => ApplyDesktopEvent(value, allowSpeech: false));
            }
        }
    }
}
