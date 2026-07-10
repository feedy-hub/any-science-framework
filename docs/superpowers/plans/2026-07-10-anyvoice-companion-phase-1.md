# AnyVoice Companion Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a buildable and tested Windows foundation containing the AnyVoice event contract, speech-text sanitizer, current-user Named Pipe transport, user data paths, and a minimal transparent WPF host.

**Architecture:** A dependency-free protocol library owns JSON framing, validation, filtering, and pipe naming. A core library owns the pipe server/client boundary, while a minimal WPF executable receives events and maps them to visible companion state. A console test runner exercises real production classes without requiring NuGet test packages, audio devices, models, or network access.

**Tech Stack:** C# 12, .NET 8, WPF, `System.Text.Json`, `System.IO.Pipes`, PowerShell 5.1+ build helpers.

---

### Task 1: Toolchain Bootstrap And Solution Skeleton

**Files:**
- Create: `companion/AnyVoiceCompanion.sln`
- Create: `companion/Directory.Build.props`
- Create: `companion/src/AnyVoice.Protocol/AnyVoice.Protocol.csproj`
- Create: `companion/src/AnyVoice.Core/AnyVoice.Core.csproj`
- Create: `companion/src/AnyVoice.Desktop/AnyVoice.Desktop.csproj`
- Create: `companion/tests/AnyVoice.Tests/AnyVoice.Tests.csproj`
- Create: `scripts/bootstrap_companion.ps1`

- [ ] **Step 1: Add the bootstrap preflight**

Create a PowerShell script that exits successfully when either `dotnet` on `PATH` or `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` reports an `8.0` SDK. When it is absent, explain that `-Install` uses Microsoft's official non-admin install script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap_companion.ps1 -Install
```

The script must never install implicitly. With `-Install`, download `https://dot.net/v1/dotnet-install.ps1` to a unique temporary file, run it with `-Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet" -NoPath`, delete the temporary script in `finally`, and verify the installed SDK.

- [ ] **Step 2: Verify preflight behavior**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap_companion.ps1
```

Expected before SDK installation: exit code `2` and an actionable command. Expected after installation: exit code `0` and the detected SDK version.

- [ ] **Step 3: Create the solution and project files**

Target `net8.0` for protocol and core. Target desktop and tests at `net8.0-windows` so the test runner can later reference the desktop controller; enable `<UseWPF>true</UseWPF>` only for desktop. Keep the initially empty test and desktop projects as libraries so the skeleton can compile without placeholder entry points; Task 2 changes tests to `Exe`, and Task 6 changes desktop to `WinExe`. Enable nullable references, implicit usings, warnings as errors, and deterministic builds.

- [ ] **Step 4: Verify the empty skeleton builds**

Run:

```powershell
dotnet build companion/AnyVoiceCompanion.sln --configuration Debug
```

Expected: exit code `0`, zero warnings, zero errors.

### Task 2: Test Runner And Event Contract

**Files:**
- Create: `companion/tests/AnyVoice.Tests/Program.cs`
- Create: `companion/tests/AnyVoice.Tests/TestSuite.cs`
- Create: `companion/tests/AnyVoice.Tests/ProtocolTests.cs`
- Create: `companion/src/AnyVoice.Protocol/CompanionEventType.cs`
- Create: `companion/src/AnyVoice.Protocol/CompanionEvent.cs`
- Create: `companion/src/AnyVoice.Protocol/CompanionEventValidator.cs`
- Create: `companion/src/AnyVoice.Protocol/CompanionProtocolException.cs`

- [ ] **Step 1: Write the failing protocol tests**

Cover these behaviors:

```csharp
suite.Test("valid event is accepted", () =>
{
    var value = CompanionEvent.Create(CompanionEventType.Thinking, "codex", "Reviewing files");
    CompanionEventValidator.Validate(value);
    suite.Equal(1, value.SchemaVersion);
});

suite.Test("unsupported schema is rejected", () =>
{
    var value = new CompanionEvent(2, CompanionEventType.Idle, "manual", null, DateTimeOffset.UtcNow);
    suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
});

suite.Test("invalid source is rejected", () =>
{
    var value = CompanionEvent.Create(CompanionEventType.Success, "../../bad", "done");
    suite.Throws<CompanionProtocolException>(() => CompanionEventValidator.Validate(value));
});
```

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- protocol`

Expected: build failure because the protocol types do not exist.

- [ ] **Step 3: Implement the minimal contract**

Use a sealed record with a `Create` factory that supplies schema version `1` and UTC time. Validation accepts sources matching `^[a-z][a-z0-9-]{0,31}$`, rejects default timestamps, and limits text to 8,192 characters before sanitization.

- [ ] **Step 4: Run and verify GREEN**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- protocol`

Expected: all protocol tests report `PASS` and the process exits `0`.

### Task 3: Speech Text Sanitizer

**Files:**
- Create: `companion/tests/AnyVoice.Tests/SanitizerTests.cs`
- Create: `companion/src/AnyVoice.Protocol/SpeechTextSanitizer.cs`

- [ ] **Step 1: Write failing sanitizer tests**

Test plain text preservation, credential assignment removal, bearer token removal, Windows path replacement, fenced code removal, whitespace collapse, and a 320-character output ceiling:

```csharp
suite.Equal("Build completed", SpeechTextSanitizer.Sanitize("Build completed"));
suite.Equal("token=[redacted]", SpeechTextSanitizer.Sanitize("token=abc123"));
suite.Equal("Authorization: [redacted]", SpeechTextSanitizer.Sanitize("Authorization: Bearer abc.def.ghi"));
suite.Equal("See [path]", SpeechTextSanitizer.Sanitize(@"See C:\Users\PS\secret.txt"));
suite.Equal("Summary", SpeechTextSanitizer.Sanitize("Summary\n```powershell\nGet-Secret\n```"));
```

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- sanitizer`

Expected: build failure because `SpeechTextSanitizer` does not exist.

- [ ] **Step 3: Implement ordered filtering**

Use compiled, culture-invariant regular expressions in this order: fenced code, authorization headers, credential assignments, Windows absolute paths, control characters, repeated whitespace, then length truncation. Return an empty string when no safe speech remains.

- [ ] **Step 4: Run and verify GREEN**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- sanitizer`

Expected: all sanitizer tests pass.

### Task 4: Framed JSON Codec

**Files:**
- Create: `companion/tests/AnyVoice.Tests/CodecTests.cs`
- Create: `companion/src/AnyVoice.Protocol/PipeMessageCodec.cs`

- [ ] **Step 1: Write failing codec tests**

Round-trip a Unicode event through a `MemoryStream`, reject a zero-length frame, reject a declared frame larger than 65,536 bytes before allocation, reject truncated data, and reject malformed JSON.

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- codec`

Expected: build failure because the codec does not exist.

- [ ] **Step 3: Implement the codec**

Write a four-byte little-endian payload length followed by compact UTF-8 JSON. Read exactly four header bytes, validate `1..65536`, then read exactly the declared payload. Use source-generated or reflection-based `System.Text.Json` with camel-case properties and string enum values. Convert parsing and validation failures into `CompanionProtocolException` without including raw payload text in the error.

- [ ] **Step 4: Run and verify GREEN**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- codec`

Expected: all codec tests pass.

### Task 5: User-Only Named Pipe Transport

**Files:**
- Create: `companion/tests/AnyVoice.Tests/PipeTransportTests.cs`
- Create: `companion/src/AnyVoice.Core/CompanionPipeNames.cs`
- Create: `companion/src/AnyVoice.Core/CompanionPipeServer.cs`
- Create: `companion/src/AnyVoice.Core/CompanionPipeClient.cs`
- Create: `companion/src/AnyVoice.Core/CompanionAcknowledgement.cs`
- Create: `companion/src/AnyVoice.Core/CompanionAcknowledgementCodec.cs`

- [ ] **Step 1: Write the failing integration test**

Start a real server with a unique test suffix, send a `success` event from the client, assert the handler receives sanitized text, and assert the acknowledgement is successful. Add a cancellation test proving the server exits within two seconds.

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- pipe`

Expected: build failure because transport classes do not exist.

- [ ] **Step 3: Implement current-user transport**

Create the server with `PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly`, one connection at a time, and a fresh server stream for each accepted client. Derive the production pipe name from the current Windows SID hashed with SHA-256; do not expose the SID itself. The client uses a two-second connection timeout and treats a missing companion as a normal unavailable result.

- [ ] **Step 4: Run and verify GREEN**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- pipe`

Expected: all transport integration tests pass without opening a network port.

### Task 6: Local Paths And Minimal WPF Host

**Files:**
- Create: `companion/tests/AnyVoice.Tests/LocalPathTests.cs`
- Create: `companion/src/AnyVoice.Core/CompanionPaths.cs`
- Create: `companion/src/AnyVoice.Desktop/App.xaml`
- Create: `companion/src/AnyVoice.Desktop/App.xaml.cs`
- Create: `companion/src/AnyVoice.Desktop/MainWindow.xaml`
- Create: `companion/src/AnyVoice.Desktop/MainWindow.xaml.cs`
- Create: `companion/src/AnyVoice.Desktop/DesktopEventController.cs`
- Create: `companion/src/AnyVoice.Desktop/DesktopDisplayState.cs`
- Modify: `companion/src/AnyVoice.Desktop/AnyVoice.Desktop.csproj`

- [ ] **Step 1: Write failing path and state tests**

Verify an injected local application data root produces only `AnyVoiceCompanion/config.json`, `characters`, and `logs` children. Verify `DesktopEventController` maps `thinking`, `speaking`, `success`, `needsInput`, and `error` to deterministic display state and always uses sanitized subtitle text.

- [ ] **Step 2: Run and verify RED**

Run: `dotnet run --project companion/tests/AnyVoice.Tests -- desktop`

Expected: build failure because paths and controller types do not exist.

- [ ] **Step 3: Implement paths and controller**

Keep the controller free of WPF types. It exposes the current event type and subtitle through an event callback. The application starts the pipe server, marshals updates onto the WPF dispatcher, and cancels the server during shutdown.

- [ ] **Step 4: Implement the minimal transparent window**

Change the desktop project output type to `WinExe`. Use a transparent, borderless, topmost window with a stable 180 by 230 logical-pixel character area, a simple built-in visual, a subtitle surface, and left-button drag handling. Do not add tray, microphone, settings, autostart, or character import in Phase 1.

- [ ] **Step 5: Run tests and build**

Run:

```powershell
dotnet run --project companion/tests/AnyVoice.Tests -- all
dotnet build companion/AnyVoiceCompanion.sln --configuration Release
```

Expected: all tests pass and the WPF application compiles with zero warnings and errors.

### Task 7: Developer Commands And Phase Verification

**Files:**
- Create: `scripts/test_companion.ps1`
- Create: `scripts/run_companion.ps1`
- Create: `scripts/resolve_companion_dotnet.ps1`
- Modify: `.gitignore`
- Modify: `README.md`

- [ ] **Step 1: Add build-output exclusions**

Ignore `companion/**/bin/`, `companion/**/obj/`, `.dotnet/`, and local companion diagnostic files without ignoring source character assets.

- [ ] **Step 2: Add deterministic developer commands**

`test_companion.ps1` runs the test console and Release build with `-NoRestore` after an explicit restore. `run_companion.ps1` verifies the SDK and starts the desktop project. Both resolve repository paths from `$PSScriptRoot` and use argument arrays.

- [ ] **Step 3: Document Phase 1 usage and boundaries**

Add Windows commands for bootstrap, test, build, and run. State clearly that Phase 1 has no microphone capture, speech output, tray icon, final character importer, Codex adapter, or Claude adapter yet.

- [ ] **Step 4: Run full verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test_companion.ps1
git diff --check
git status --short
```

Expected: the test runner and Release build exit `0`, `git diff --check` is clean, and only intended source, test, script, and documentation files are listed.
