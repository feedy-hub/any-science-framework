# AnyVoice Companion Phase 3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe local speech output, offline tool discovery, microphone-device parsing, toggle-to-dictate orchestration, and Whisper transcription while reusing installed tools and cached models only.

**Architecture:** Voice orchestration depends on small process, recorder, transcriber, and speech interfaces so all automated tests use fakes. Production adapters call Windows PowerShell/System.Speech, FFmpeg DirectShow, and Whisper with argument arrays and offline environment variables. A disabled-by-default global hotkey toggles recording; microphone capture never starts from a pipe event.

**Tech Stack:** C# 12, .NET 8, Windows PowerShell/System.Speech, FFmpeg DirectShow, OpenAI Whisper CLI, Win32 `RegisterHotKey`.

---

### Task 1: Voice Status And Tool Discovery

**Files:**
- Create: `companion/src/AnyVoice.Core/Voice/VoiceToolPaths.cs`
- Create: `companion/src/AnyVoice.Core/Voice/VoiceToolDiscovery.cs`
- Create: `companion/src/AnyVoice.Core/Voice/VoiceStatus.cs`
- Create: `companion/src/AnyVoice.Core/Voice/FfmpegDeviceParser.cs`
- Create: `companion/tests/AnyVoice.Tests/VoiceDiscoveryTests.cs`

- [ ] Write failing tests for explicit path precedence, PATH discovery, missing executable handling, complete `~/.cache/whisper/*.pt` model discovery, partial-file rejection, and FFmpeg DirectShow microphone parsing.
- [ ] Implement discovery using filesystem APIs only. Do not scan entire drives, install packages, or call a network endpoint.
- [ ] Return a structured status containing usable FFmpeg, Whisper, cached model, microphones, and actionable errors.

### Task 2: Safe Process Boundary And TTS

**Files:**
- Create: `companion/src/AnyVoice.Core/Voice/ProcessRequest.cs`
- Create: `companion/src/AnyVoice.Core/Voice/ProcessResult.cs`
- Create: `companion/src/AnyVoice.Core/Voice/IProcessRunner.cs`
- Create: `companion/src/AnyVoice.Core/Voice/ProcessRunner.cs`
- Create: `companion/src/AnyVoice.Core/Voice/ISpeechOutput.cs`
- Create: `companion/src/AnyVoice.Core/Voice/PowerShellSpeechOutput.cs`
- Create: `companion/tests/AnyVoice.Tests/SpeechOutputTests.cs`

- [ ] Write failing tests proving speech text is sanitized and sent through standard input, never interpolated into the PowerShell command or arguments.
- [ ] Implement a bounded process runner with argument lists, standard-input data, explicit environment variables, timeout, cancellation, and captured output limits.
- [ ] Implement TTS with `powershell.exe -NoProfile -NonInteractive`, `System.Speech`, and stdin text. Empty sanitized text is ignored.
- [ ] Use a fake process runner in tests; do not produce real audio in automated verification.

### Task 3: Offline Whisper Transcription

**Files:**
- Create: `companion/src/AnyVoice.Core/Voice/ITranscriber.cs`
- Create: `companion/src/AnyVoice.Core/Voice/WhisperTranscriber.cs`
- Create: `companion/tests/AnyVoice.Tests/WhisperTranscriberTests.cs`

- [ ] Write failing tests for spaced paths, explicit cached model selection, UTF-8 output, transcript-file containment, timeout, and non-zero exit handling.
- [ ] Invoke Whisper with an argument array, `--model <cached-name>`, `--model_dir <verified-cache>`, `--output_format txt`, and a unique temporary output directory.
- [ ] Set `HF_HUB_OFFLINE=1`, `TRANSFORMERS_OFFLINE=1`, `PYTHONUTF8=1`, and `PYTHONIOENCODING=utf-8`.
- [ ] Reject absent models and never substitute a default model that could download.

### Task 4: Recorder And Dictation State Machine

**Files:**
- Create: `companion/src/AnyVoice.Core/Voice/IAudioRecorder.cs`
- Create: `companion/src/AnyVoice.Core/Voice/FfmpegAudioRecorder.cs`
- Create: `companion/src/AnyVoice.Core/Voice/DictationController.cs`
- Create: `companion/tests/AnyVoice.Tests/DictationControllerTests.cs`

- [ ] Write failing fake-adapter tests for idle-to-recording, recording-to-transcribing, success, cancellation, recorder failure, transcriber failure, and re-entrant toggle rejection.
- [ ] Start FFmpeg with DirectShow, one configured microphone, mono 16 kHz WAV, and a unique temporary file. Stop by writing `q` to stdin and wait with a bounded timeout.
- [ ] Delete the WAV after successful or failed transcription unless diagnostic retention is enabled.
- [ ] Emit desktop `listening`, `thinking`, `success`, and `error` events without ever executing transcript text.

### Task 5: Hotkey, UI, And Speech Policy

**Files:**
- Create: `companion/src/AnyVoice.Desktop/GlobalHotkey.cs`
- Create: `companion/src/AnyVoice.Desktop/VoiceUiController.cs`
- Modify: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml`
- Modify: `companion/src/AnyVoice.Desktop/SettingsWindow.xaml.cs`
- Modify: `companion/src/AnyVoice.Desktop/App.xaml.cs`
- Modify: `companion/src/AnyVoice.Desktop/TrayIconController.cs`

- [ ] Register `Ctrl+Alt+V` only when the user enables the hotkey. Registration failure disables it and shows an actionable status.
- [ ] First hotkey press starts recording; the second stops and transcribes. Tray commands expose the same explicit toggle.
- [ ] Speak only sanitized `success`, `needsInput`, and `error` summaries when speech is enabled. Never speak `listening` or raw transcripts automatically.
- [ ] Show discovered tools, model, microphone, voice enablement, and hotkey status in the Voice settings tab.

### Task 6: Phase 3 Verification

**Files:**
- Modify: `README.md`
- Modify: `scripts/test_companion.ps1`

- [ ] Run all tests and Release build without microphone capture, model loading, speech playback, or network access.
- [ ] Launch the desktop app, send safe state events, exercise TTS through a fake/manual opt-in command, and verify tray/settings remain responsive.
- [ ] Verify actual local FFmpeg, Whisper, `tiny.pt`, and microphone are reported as reusable, while downloads remain disabled.
- [ ] Do not run a real microphone recording until the user gives a separate explicit approval.
