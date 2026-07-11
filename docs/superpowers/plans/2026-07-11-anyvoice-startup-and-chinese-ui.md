# AnyVoice Startup And Chinese UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional, disabled-by-default current-user Windows startup setting and make Simplified Chinese the default language for the existing desktop UI.

**Architecture:** Keep startup policy testable in `AnyVoice.Core` through a value-store interface and a command builder. Put the `Microsoft.Win32` registry adapter in the Windows desktop project, then reconcile registration through the existing settings save path. Translate only static user-visible text; protocol and configuration contracts remain English.

**Tech Stack:** C# 12, .NET 8, WPF, `Microsoft.Win32.Registry`, the existing custom offline test suite.

---

### Task 1: Settings Contract

**Files:**
- Modify: `companion/src/AnyVoice.Core/CompanionSettings.cs`
- Modify: `companion/tests/AnyVoice.Tests/SettingsTests.cs`

- [ ] Add a failing assertion that `CompanionSettings.Default.StartWithWindows` is `false` and a round-trip assertion that `true` survives JSON storage.
- [ ] Run `dotnet run --project companion/tests/AnyVoice.Tests -- settings` and verify compilation fails because the property does not exist.
- [ ] Add `public bool StartWithWindows { get; init; }` without changing the schema version; missing JSON properties naturally deserialize to `false`.
- [ ] Re-run the settings group and verify it passes.

### Task 2: Startup Command And Registration Policy

**Files:**
- Create: `companion/src/AnyVoice.Core/Startup/IStartupValueStore.cs`
- Create: `companion/src/AnyVoice.Core/Startup/StartupLaunchCommand.cs`
- Create: `companion/src/AnyVoice.Core/Startup/StartupRegistrationService.cs`
- Create: `companion/src/AnyVoice.Core/Startup/StartupRegistrationException.cs`
- Create: `companion/tests/AnyVoice.Tests/StartupRegistrationTests.cs`
- Modify: `companion/tests/AnyVoice.Tests/Program.cs`

- [ ] Write failing tests for quoted absolute paths, absent-path rejection, enable, disable, idempotence, stale-command replacement, and store failure wrapping.
- [ ] Run the `startup` test group and verify RED because the startup types do not exist.
- [ ] Implement `StartupLaunchCommand.Build(dotnetPath, assemblyPath)` to require two existing absolute files and return `"dotnet" "assembly"` without shell syntax.
- [ ] Implement `StartupRegistrationService` with the fixed value name `AnyVoiceCompanion`; enable writes only when the exact command differs, disable removes only that value, and adapter failures become `StartupRegistrationException`.
- [ ] Re-run the startup group and verify GREEN.

### Task 3: Windows Registry Adapter And Application Wiring

**Files:**
- Create: `companion/src/AnyVoice.Desktop/WindowsStartupValueStore.cs`
- Create: `companion/src/AnyVoice.Desktop/CurrentApplicationStartupCommand.cs`
- Modify: `companion/src/AnyVoice.Desktop/App.xaml.cs`

- [ ] Implement a current-user adapter for `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` using `Registry.CurrentUser.CreateSubKey`; get, set, and delete only the requested value.
- [ ] Resolve `dotnet.exe` from the currently loaded .NET runtime root and resolve the entry assembly DLL from `Assembly.GetEntryAssembly().Location`.
- [ ] Create the startup service during app initialization and reconcile the saved setting after the UI event controller exists.
- [ ] In `ApplySettings`, update the registry before saving JSON. On failure, retain the previous in-memory/settings-file value and show a Chinese warning.
- [ ] Build the solution and fix all warnings or errors.

### Task 4: Chinese Default UI

**Files:**
- Modify: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml`
- Modify: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml.cs`
- Modify: `companion/src/AnyVoice.Desktop/TrayIconController.cs`
- Modify: `companion/src/AnyVoice.Desktop/MainWindow.xaml`
- Modify: `companion/src/AnyVoice.Desktop/MainWindow.xaml.cs`
- Modify: `companion/src/AnyVoice.Desktop/DesktopEventController.cs`
- Modify: `companion/src/AnyVoice.Desktop/App.xaml.cs`
- Modify: `companion/src/AnyVoice.Core/Voice/DictationController.cs`
- Modify: `companion/tests/AnyVoice.Tests/DesktopControllerTests.cs`
- Modify: `companion/tests/AnyVoice.Tests/DictationControllerTests.cs`

- [ ] Update failing tests to expect Chinese fallback statuses and Chinese stable dictation summaries while retaining source/type semantics.
- [ ] Translate all static visible WPF, tray, warning, readiness, and state-label text to Simplified Chinese.
- [ ] Add the General-tab `StartWithWindowsCheckBox` with Chinese text and bind it to `CompanionSettings.StartWithWindows`.
- [ ] Keep protocol enums, JSON names, registry names, and tool paths unchanged.
- [ ] Run desktop, settings, dictation, speech, and startup groups.

### Task 5: Verification And Delivery

**Files:**
- Modify: `README.md`
- Modify: `scripts/test_companion.ps1` only if verification coverage needs adjustment.

- [ ] Document the default-off startup toggle, HKCU scope, Chinese default UI, and disable behavior.
- [ ] Stop the running Companion instance, run `scripts/test_companion.ps1`, and require all tests plus Release build to pass with zero warnings.
- [ ] Back up the exact `AnyVoiceCompanion` Run value, test enable and disable through the production adapter or UI, then restore the original exact value.
- [ ] Launch the final Release app, verify a visible Chinese window, and leave the startup option disabled unless the user enables it.
- [ ] Run `git diff --check`, verify no user registry export or local config is tracked, commit on `codex/windows-native-ui-voice`, and push the same branch.
