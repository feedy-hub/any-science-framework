# Windows Native UI And Voice Design

## Goal

Make the optional Any Science UI and Voice extensions installable and usable from native Windows PowerShell without WSL, while preserving the current Bash workflow for Linux, macOS, Git Bash, and WSL users.

## Supported User Flow

From an existing Any Science workspace, a Windows user runs the PowerShell installer from the framework repository by absolute path. The installer writes only extension-owned files, backs up collisions, and produces native `.ps1` launchers. UI runs with the first working local `python.exe`; Voice records through Windows FFmpeg DirectShow, transcribes through an already installed local backend, and speaks through Windows SAPI.

No installer downloads packages or models. If the requested model is not already complete on disk, Voice exits with an actionable error.

## Architecture

The Bash and PowerShell installers generate equivalent extension assets. The UI backend remains a standard-library Python server shared by both platforms. Platform-specific process management, audio capture, STT invocation, and TTS live in generated shell or PowerShell launchers.

Release sources live under `src/extensions/`; `scripts/build.ps1` copies PowerShell sources to `dist/` using UTF-8 without BOM and LF line endings. Existing `scripts/build.sh` also copies both `.sh` and `.ps1` release files so either development environment can build the complete distribution.

## Windows UI

- `setup_ui.ps1` verifies `CLAUDE.md`, `PROTOCOL.md`, `.claude/settings.json`, and `scripts/validate.sh`.
- It writes the same `ui/server.py`, `ui/static/index.html`, and UI agent files as the Bash installer, plus `scripts/ui_start.ps1` and `scripts/ui_stop.ps1`.
- `ui_start.ps1` resolves paths from its own location, selects a real Python interpreter, starts it hidden, stores its PID, waits for `/api/overview`, and optionally opens the browser.
- `ui_stop.ps1` checks the PID process command line before stopping it.
- The server rejects invalid `Content-Length`, negative or oversized bodies, non-object JSON, and non-string messages with HTTP 400/411/413 responses.
- The UI displays network failures and uses a full-width details panel on narrow screens.

## Windows Voice

- `setup_voice.ps1` writes `say.ps1`, `stt.ps1`, `dictate.ps1`, `voice_status.ps1`, and a local JSON configuration template.
- `voice_status.ps1` discovers FFmpeg, microphone devices, Python/Whisper executables, CUDA, and complete local model files without changing the machine.
- `dictate.ps1` records WAV with FFmpeg DirectShow. It selects the sole microphone automatically; with multiple devices it requires `ANY_SCIENCE_AUDIO_DEVICE` or a configuration value.
- `stt.ps1` invokes commands with PowerShell argument arrays. It supports OpenAI Whisper CLI and an explicit executable adapter, sets UTF-8 environment variables, and forces offline mode.
- OpenAI Whisper CLI chooses a model only when the matching `.pt` file already exists. On this machine that means `tiny.pt`; it must never fall back to the absent default `turbo` model.
- Hugging Face cache directories count as usable only when a snapshot contains actual model files, not merely `refs/main`.
- Confirmed transcriptions are written only to `workspace/inbox/` as semi-trusted requests.
- `say.ps1` uses `System.Speech` and passes the text as data.

## Safety And Compatibility

- All generated paths are resolved with .NET or Python path APIs; Unix placeholder paths are not used in Windows instructions.
- Existing files receive timestamped sibling backups before replacement.
- UI binds only to `127.0.0.1` and retains Host, Origin, and workspace containment checks.
- Voice never uses `Invoke-Expression`, `cmd /c`, `eval`, or string-built executable commands.
- Linux/macOS behavior remains available through existing `.sh` files.

## Tests

PowerShell tests create temporary workspaces under the Windows temporary directory, run native installers, compile and launch the UI, call its HTTP API, and test path and payload rejection. Voice tests use a fake executable adapter and a generated WAV fixture so they validate argument handling and inbox writes without recording, downloading, or loading a real model.

The existing WSL smoke suite remains mandatory to prevent cross-platform regressions.
