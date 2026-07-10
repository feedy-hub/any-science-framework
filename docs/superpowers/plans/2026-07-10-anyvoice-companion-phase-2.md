# AnyVoice Companion Phase 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Phase 1 WPF prototype into a stable current-user desktop application with persisted settings, single-instance activation, subtitle behavior, tray controls, and a compact settings window.

**Architecture:** Configuration and single-instance policy remain testable outside WPF. The WPF application owns window placement and tray lifetime, while the existing event controller remains the only state-to-display mapper. Settings are stored atomically below `%LOCALAPPDATA%\AnyVoiceCompanion` and never modify projects or machine-wide configuration.

**Tech Stack:** C# 12, .NET 8 WPF, Windows Forms `NotifyIcon`, `System.Text.Json`, Windows named mutexes.

---

### Task 1: Settings Contract And Atomic Store

**Files:**
- Create: `companion/src/AnyVoice.Core/CompanionSettings.cs`
- Create: `companion/src/AnyVoice.Core/CompanionSettingsStore.cs`
- Create: `companion/tests/AnyVoice.Tests/SettingsTests.cs`
- Modify: `companion/tests/AnyVoice.Tests/Program.cs`

- [ ] Write failing tests for defaults, invalid-value normalization, JSON round-trip, missing-file fallback, malformed-file quarantine, and atomic save.
- [ ] Run `dotnet run --project companion/tests/AnyVoice.Tests -- settings` and verify RED because the settings types do not exist.
- [ ] Implement immutable settings with defaults: scale `1.0`, opacity `1.0`, subtitles enabled, speech enabled, hotkey disabled, no explicit audio tools, and nullable window coordinates.
- [ ] Implement UTF-8 JSON load/save using a sibling temporary file and `File.Move(..., overwrite: true)`. Malformed files receive a timestamped `.invalid-*` sibling and defaults are returned.
- [ ] Re-run the settings group and verify GREEN.

### Task 2: Single-Instance Coordination

**Files:**
- Create: `companion/src/AnyVoice.Core/SingleInstanceCoordinator.cs`
- Create: `companion/tests/AnyVoice.Tests/SingleInstanceTests.cs`

- [ ] Write a failing test that creates a unique mutex name, verifies the first coordinator owns it, verifies the second does not, disposes the first, and verifies a third can acquire it.
- [ ] Implement a current-user name derived from the same SID digest used for the pipe, and an injectable test name.
- [ ] Verify the test group passes and abandoned mutexes are treated as acquired.

### Task 3: Window Persistence And Subtitle Lifetime

**Files:**
- Create: `companion/src/AnyVoice.Desktop/DesktopSettingsController.cs`
- Modify: `companion/src/AnyVoice.Desktop/MainWindow.xaml`
- Modify: `companion/src/AnyVoice.Desktop/MainWindow.xaml.cs`
- Test: `companion/tests/AnyVoice.Tests/DesktopSettingsTests.cs`

- [ ] Write failing pure-controller tests for scale/opacity clamping, off-screen coordinate rejection, subtitle visibility, and persistent `needsInput`/`error` states.
- [ ] Implement a controller free of WPF types that merges settings with display state and emits deterministic presentation values.
- [ ] Bind window size, opacity, subtitle visibility, and placement to the controller. Save placement after drag and hide non-persistent subtitles after the configured duration.
- [ ] Keep right-click exit and add a settings command without nesting card surfaces.

### Task 4: Tray And Settings Window

**Files:**
- Modify: `companion/src/AnyVoice.Desktop/AnyVoice.Desktop.csproj`
- Create: `companion/src/AnyVoice.Desktop/TrayIconController.cs`
- Create: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml`
- Create: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml.cs`
- Modify: `companion/src/AnyVoice.Desktop/App.xaml.cs`

- [ ] Enable Windows Forms support without adding a NuGet package.
- [ ] Add one tray icon with Show/Hide, Subtitles, Speech, Settings, and Exit commands. Dispose the icon during shutdown.
- [ ] Add a compact settings window with General and Voice tabs. Phase 2 fields edit scale, opacity, subtitles, speech, and subtitle duration; Phase 3 fills the Voice tab.
- [ ] Save valid changes atomically and apply them without restarting.

### Task 5: Phase 2 Verification

**Files:**
- Modify: `README.md`
- Modify: `scripts/test_companion.ps1`

- [ ] Document tray behavior, settings location, and current Phase 2 boundaries.
- [ ] Run all tests and a Release build.
- [ ] Launch one desktop process, launch a second and verify it exits while activating the first, verify one visible character window and one tray icon process, then exit through the application command.
- [ ] Run `git diff --check` and confirm no settings file or user path is tracked.
