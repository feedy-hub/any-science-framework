# Windows Native UI And Voice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a native PowerShell installation and runtime path for Any Science UI and Voice that reuses local Windows tools and never downloads models.

**Architecture:** Keep the Python UI server platform-neutral and generate platform-specific launchers from paired Bash and PowerShell extension installers. Add a native PowerShell build and smoke suite while retaining the current WSL tests as compatibility coverage.

**Tech Stack:** Windows PowerShell 5.1+, Python 3 standard library, FFmpeg DirectShow, OpenAI Whisper CLI, Bash/WSL compatibility tests.

---

### Task 1: Native Build And Test Harness

**Files:**
- Create: `scripts/build.ps1`
- Create: `tests/windows_extensions_smoke.ps1`
- Modify: `scripts/build.sh`

- [ ] **Step 1: Write a failing release test**

Assert that `dist/extensions/setup_ui.ps1` and `dist/extensions/setup_voice.ps1` exist after `scripts/build.ps1`, contain no CRLF, and parse with the PowerShell parser:

```powershell
$errors = $null
[void][Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$errors)
if ($errors.Count) { throw "$path has parse errors" }
```

- [ ] **Step 2: Run the test and verify RED**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/windows_extensions_smoke.ps1`

Expected: failure because `scripts/build.ps1` and PowerShell extension installers do not exist.

- [ ] **Step 3: Implement the minimal native build**

`scripts/build.ps1` must copy `src/setup*.ps1` when present and every `src/extensions/*.ps1` into `dist/`, normalize newlines to LF, write UTF-8 without BOM, and parse each destination. `scripts/build.sh` must copy both extension suffixes.

- [ ] **Step 4: Verify the build portion passes**

Run the PowerShell smoke test and confirm it advances to the missing installer behavior assertion.

### Task 2: Native UI Installer And Runtime

**Files:**
- Create: `src/extensions/setup_ui.ps1`
- Generate: `dist/extensions/setup_ui.ps1`
- Modify: `src/extensions/setup_ui.sh`
- Test: `tests/windows_extensions_smoke.ps1`

- [ ] **Step 1: Add failing native UI assertions**

Create a temporary Any Science workspace fixture, run `setup_ui.ps1`, and assert these files exist:

```powershell
@(
  'ui/server.py',
  'ui/static/index.html',
  'scripts/ui_start.ps1',
  'scripts/ui_stop.ps1'
) | ForEach-Object { Assert-True (Test-Path (Join-Path $project $_)) "missing $_" }
```

Launch `ui_start.ps1 -NoBrowser -Port 18321`, verify `/api/overview` returns 200, verify `../CLAUDE.md` returns 403, and verify JSON `[]` returns 400.

- [ ] **Step 2: Run the UI test and verify RED**

Expected: failure because the native installer is absent or does not generate native launchers.

- [ ] **Step 3: Implement the native UI installer**

Use `Set-Content` through a UTF-8-no-BOM helper, timestamped backups, and `$PSScriptRoot`-based path resolution. Generated start/stop scripts must validate their process and wait on HTTP readiness instead of sleeping for a fixed duration.

- [ ] **Step 4: Harden the shared UI server**

Make body parsing return structured errors:

```python
try:
    size = int(self.headers["Content-Length"])
except (KeyError, TypeError, ValueError):
    return self.send_json(411, {"error": "valid Content-Length required"})
if size < 0:
    return self.send_json(400, {"error": "Content-Length must not be negative"})
if size > 20000:
    return self.send_json(413, {"error": "message too large"})
payload = json.loads(self.rfile.read(size))
if not isinstance(payload, dict):
    return self.send_json(400, {"error": "expected a JSON object"})
```

Add frontend fetch error reporting and a narrow-screen media query.

- [ ] **Step 5: Run native and WSL UI tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/windows_extensions_smoke.ps1 -UIOnly
wsl.exe -- bash -lc "cd '<worktree>' && bash tests/extensions_smoke.sh"
```

Expected: both pass.

### Task 3: Native Voice Installer And Offline Adapters

**Files:**
- Create: `src/extensions/setup_voice.ps1`
- Generate: `dist/extensions/setup_voice.ps1`
- Modify: `src/extensions/setup_voice.sh`
- Test: `tests/windows_extensions_smoke.ps1`

- [ ] **Step 1: Add failing native Voice assertions**

Run the native installer in a temporary fixture and assert `say.ps1`, `stt.ps1`, `dictate.ps1`, and `voice_status.ps1` parse. Use a fake STT executable script that records received arguments and writes a transcript. Assert paths containing spaces are passed as one argument and no network command appears in generated scripts.

- [ ] **Step 2: Run the Voice test and verify RED**

Expected: failure because `setup_voice.ps1` does not exist.

- [ ] **Step 3: Implement discovery and offline STT**

Detect `whisper.exe`, `ffmpeg.exe`, `%USERPROFILE%\.cache\whisper\*.pt`, and complete Hugging Face snapshots. Set `PYTHONUTF8=1`, `PYTHONIOENCODING=utf-8`, `HF_HUB_OFFLINE=1`, and `TRANSFORMERS_OFFLINE=1` for child processes. Select an explicit cached `.pt` model and call Whisper with an argument array.

- [ ] **Step 4: Implement DirectShow recording and SAPI output**

Parse FFmpeg device enumeration, use `-f dshow -i audio=<device>`, write WAV under `[IO.Path]::GetTempPath()`, confirm transcription, and write an LF UTF-8 inbox file. SAPI receives text directly through its `.Speak()` method.

- [ ] **Step 5: Keep Bash safe and honest**

Update Bash documentation and status output so WSL users are warned that Linux audio and Windows executables are separate environments. Prevent default Whisper model downloads by requiring an existing model choice or explicit command.

- [ ] **Step 6: Run native and WSL Voice tests**

Expected: fake-adapter tests pass without recording or model loading; Bash syntax and no-download tests remain green.

### Task 4: Documentation And Full Verification

**Files:**
- Modify: `README.md`
- Modify: `README.en.md`
- Modify: `README.zh-TW.md`
- Modify: `.gitattributes`

- [ ] **Step 1: Add Windows-first commands**

Document repository and workspace absolute-path examples using PowerShell call syntax:

```powershell
Set-Location 'D:\fu_files\工作\其他\any-science-workspace'
& 'D:\fu_files\工作\其他\any-science-framework-dev\dist\extensions\setup_ui.ps1'
.\scripts\ui_start.ps1
```

Document offline Voice status, microphone selection, cached model selection, and Bash alternatives. Remove the contradictory claim that no GUI exists.

- [ ] **Step 2: Run full verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/windows_extensions_smoke.ps1
wsl.exe -- bash -lc "cd '<worktree>' && bash tests/smoke.sh"
git diff --check
git status --short
```

Expected: all commands exit 0, tests report success, and no unintended files are present.

- [ ] **Step 3: Review the final diff**

Check every generated executable for path quoting, absence of download/install commands, localhost-only UI binding, and writes constrained to `workspace/inbox/` or extension-owned metadata.
