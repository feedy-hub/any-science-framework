# AnyVoice Companion Design

## Goal

Build a project-independent Windows desktop voice companion that can receive events from Codex, Claude Code, and future tools, present a customizable always-on-top character, and provide local speech output and push-to-talk transcription without requiring WSL.

The companion is distributed from this repository but installs into the current Windows user's profile. It does not require an Any Science workspace and does not replace the existing workspace-scoped Voice extension.

## Product Boundary

- The desktop companion is a standalone Windows application with a transparent, borderless character window and a system tray process.
- Codex and Claude Code use thin user-level adapters. Audio, filtering, configuration, and character state remain in the shared companion.
- The first release supports frame-based PNG/WebP character packs. Live2D is an optional later renderer behind the same state contract.
- Existing local FFmpeg, Whisper CLI, and cached Whisper models are reused. The companion never downloads a speech model.
- Continuous microphone listening is disabled. Speech recognition starts only from an explicit command or configured push-to-talk hotkey.
- The first development phase delivers the protocol, secure local transport, filtering, configuration paths, and a minimal WPF character shell. Tray controls, audio, full character import, and adapters follow as separate phases.

## Architecture

```text
Codex Skill / MCP adapter ----\
                               >-- user-only Named Pipe --> AnyVoice Desktop Host
Claude Code plugin / hooks ---/                              |-- event sanitizer
                                                             |-- state reducer
                                                             |-- WPF character window
                                                             |-- TTS / STT adapters
                                                             `-- local configuration
```

The desktop host owns the Named Pipe server. Adapters are clients that send a length-prefixed UTF-8 JSON event and wait for an acknowledgement. No TCP listener, HTTP endpoint, administrator service, or cross-user IPC is created.

## Event Contract

Every event contains:

- `schemaVersion`: integer protocol version, initially `1`.
- `type`: `idle`, `listening`, `thinking`, `speaking`, `success`, `needsInput`, or `error`.
- `source`: a short adapter identifier such as `codex`, `claude`, or `manual`.
- `text`: optional text proposed for the subtitle and speech pipeline.
- `createdAtUtc`: ISO 8601 UTC timestamp.

The host rejects unsupported versions, unknown event types, invalid sources, payloads larger than 64 KiB, and malformed JSON. It sanitizes text again even if the adapter claims it already filtered the content.

## Windows Runtime

- **UI:** .NET 8 WPF, transparent and borderless, with `Topmost` enabled and explicit drag handling.
- **IPC:** `NamedPipeServerStream` and `NamedPipeClientStream` with `PipeOptions.CurrentUserOnly` and asynchronous I/O.
- **Single instance:** one host per Windows user. A second process sends an activation event and exits.
- **Configuration:** `%LOCALAPPDATA%\AnyVoiceCompanion\config.json`.
- **Character data:** `%LOCALAPPDATA%\AnyVoiceCompanion\characters\`.
- **Logs:** `%LOCALAPPDATA%\AnyVoiceCompanion\logs\`, disabled by default for message bodies and rotated when enabled.
- **Installation:** user-level files only. No registry autorun is created unless the user enables it in settings.

## Character Model

A character pack is a directory or ZIP containing `character.json`, `preview.png`, and state directories. Each state may contain one static image or ordered animation frames. Missing states fall back to `idle`.

```text
my-character/
|-- character.json
|-- preview.png
|-- idle/
|-- listening/
|-- thinking/
|-- speaking/
|-- success/
|-- needs-input/
`-- error/
```

The manifest stores a stable identifier, display name, author, format version, frame timing, default scale, anchor point, and state mappings. Imported paths are resolved under the extracted character directory; absolute paths and parent traversal are rejected.

## Interaction Design

- Dragging moves the character unless its position is locked.
- Right-click opens the companion menu. The tray icon exposes the same core commands.
- The user can show or hide subtitles, mute speech, start push-to-talk, open settings, or exit.
- Click-through can be enabled after the character position is locked.
- Subtitle text wraps and disappears after a configurable duration. `needsInput` and `error` remain visible until acknowledged.
- Size, position, opacity, voice, speech rate, volume, hotkey, spoken event types, and character package are user-configurable.

## Voice Behavior

- TTS first uses installed Windows voices through a replaceable speech adapter.
- STT invokes the existing Windows FFmpeg and Whisper CLI with argument arrays, offline environment variables, and an explicitly verified cached model.
- The companion does not keep raw microphone recordings after successful transcription unless the user enables a diagnostic retention setting.
- A failed recorder, missing model, or unavailable microphone changes the character to `error` but does not crash Codex, Claude Code, or the desktop host.

## Privacy And Security

- Named Pipe access is restricted to the current Windows user.
- The host limits frame size before allocating payload buffers and uses timeouts and cancellation for every connection.
- Proposed speech text is filtered for credential assignments, authorization headers, private keys, URLs with embedded credentials, full local paths, large code blocks, and excessive length.
- Raw command output is not spoken automatically. Adapters send a short summary instead.
- Microphone capture is never started from an untrusted event payload.
- Adapter events are data only. They cannot specify executable paths, PowerShell commands, character file paths, or arbitrary settings mutations.
- Application failure is isolated: all adapters treat a missing companion as a non-fatal notification failure.

## Codex And Claude Integration

The Codex adapter is a user-level Skill plus a local MCP command surface. It supports explicit status and speech events without relying on private Codex files. Automatic lifecycle coverage depends on public Codex extension points available at installation time.

The Claude Code adapter is a user-level plugin. Safe lifecycle hooks translate supported events such as session start, notification, stop, and permission prompts into the shared protocol. Hook failure always exits successfully so it cannot block Claude Code.

## Compatibility

- Windows 10/11 x64 is the initial target.
- WSL is not required.
- No network port is opened.
- Existing Any Science UI on `127.0.0.1:8321` is unaffected.
- Existing workspace `scripts/voice/*.ps1` files are not overwritten. A later optional bridge may delegate their TTS and STT operations to the companion.

## Delivery Phases

1. **Foundation:** protocol, text sanitizer, user-only Named Pipe transport, local paths, minimal WPF host, and test harness.
2. **Desktop experience:** tray process, settings, persistence, state reducer, subtitles, window controls, and single-instance activation.
3. **Voice:** Windows TTS, existing FFmpeg/Whisper discovery, push-to-talk, offline transcription, and diagnostic status.
4. **Character packs:** manifest validation, safe ZIP import, animation renderer, built-in character, and customization UI.
5. **Adapters:** Codex Skill/MCP package, Claude Code plugin/hooks, installers, uninstallers, and end-to-end diagnostics.

## Verification Strategy

- Protocol and sanitizer unit tests run without microphone, model, UI interaction, or network access.
- Named Pipe integration tests create a real current-user server and client and cover malformed and oversized payload rejection.
- The WPF project must compile on Windows and its view model is tested separately from the window.
- Audio tests use fake process adapters until the user explicitly approves a real microphone test.
- Installation tests use temporary user-profile roots and verify no project files, global services, ports, or unrelated configuration are modified.
