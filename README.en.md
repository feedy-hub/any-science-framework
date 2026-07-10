# Any Science Framework

<p align="center">
  <a href="./README.md">简体中文</a> |
  <a href="./README.zh-TW.md">繁體中文</a> |
  <a href="./README.en.md">English</a>
</p>

Any Science Framework is a self-bootstrapping AI assistant framework for research work. It is not a single-domain paper assistant. It is a local project generator that first specializes itself to a research domain, then runs a closed loop around ideas, experiments, reviews, analysis, and iteration.

This repository contains the framework development package. You can use it to generate a new Any Science research workspace, or continue developing the framework itself with build scripts, release scripts, and regression tests.

## What Problem Does It Solve?

AI research assistants often become unreliable in a few predictable ways:

- Domain context, experiment state, and analysis conclusions live only in chat, making them hard to recover across sessions.
- Ideas, experiment designs, and analyses do not follow a strict state machine, which makes it easy to skip steps or move success criteria after seeing results.
- Review is often a verbal reminder rather than an enforced file contract.
- Numeric conclusions may not be traceable back to structured result files.
- A setup script may work once, but without build, test, and release workflow it becomes hard to maintain.

Any Science Framework turns these constraints into project structure, protocol files, hooks, validators, and acceptance tests instead of relying only on prompt discipline.

## Key Features

- Domain-neutral research loop: `IDEA -> DESIGN -> APPROVED -> RUNNING -> ANALYZED -> ITERATE / PROMOTE / KILLED`
- Domain specialization entrypoint: every new workspace starts with `/build` to create the domain profile, domain skills, and reviewer checklist.
- Protocol-first design: `PROTOCOL.md` is the final authority for file formats, state conflicts, and error handling.
- Reviewer transcription workflow: reviewers inspect but do not edit; the PI agent transcribes review findings and updates the final `REVIEW:` line.
- Automatic validation: generated workspaces include `scripts/validate.sh` for status, REVIEW, graveyard, and metrics schema checks.
- Hook gates: generated `.claude/settings.json` configures PostToolUse and Stop hooks to block invalid outputs.
- Safety boundary: secret files and destructive commands are guarded by default, while clearly documenting that these are soft constraints and not a replacement for Docker/devcontainer isolation.
- Acceptance test kit: `setup_test.sh` generates `test-kit/` with scenarios for data leakage, fake citations, moving goalposts, noise-level improvements, and graveyard checks.
- Maintainable release flow: this repository keeps source scripts in `src/`, release scripts in `dist/`, and uses smoke tests to prevent CRLF, syntax, and protocol regressions.

## Repository Layout

```text
any-science-framework/
├── src/
│   ├── setup.sh          # Source workspace generator
│   └── setup_test.sh     # Source acceptance test-kit generator
├── dist/
│   ├── setup.sh          # Release workspace generator
│   └── setup_test.sh     # Release test-kit generator
├── scripts/
│   └── build.sh          # Builds dist from src and enforces LF line endings
├── tests/
│   └── smoke.sh          # End-to-end regression tests
├── docs/
│   ├── design.md         # Current design scope
│   └── plan.md           # Implementation plan and release checklist
├── .gitattributes        # Forces LF line endings for scripts and docs
└── README.md
```

## Quick Start

### 1. Clone The Repository

```bash
git clone https://github.com/feedy-hub/any-science-framework.git
cd any-science-framework
```

### 2. Generate A New Research Workspace

Use an explicit new directory to avoid mixing the generated files with existing projects.

```bash
bash dist/setup.sh /tmp/my-any-science
cd /tmp/my-any-science
```

On Windows + WSL, you can generate the workspace on a Windows drive:

```bash
bash dist/setup.sh /mnt/d/fu_files/工作/其他/my-any-science
cd /mnt/d/fu_files/工作/其他/my-any-science
```

### 3. Make The Initial Commit

```bash
git add -A
git commit -m init
```

### 4. Start Claude And Specialize The Domain

```bash
claude
```

Then run:

```text
/build
```

`/build` starts the domain specialization interview. Before specialization, the generated PI agent intentionally refuses to perform research work.

### 5. Generate The Acceptance Test Kit

Inside the generated research workspace:

```bash
bash /path/to/any-science-framework/dist/setup_test.sh
```

Then follow `test-kit/TESTS.md`. On first use, run the additional self-checks to confirm that hooks, validators, and permission rules still behave correctly with your Claude Code version.

## What Gets Generated?

Running `dist/setup.sh` creates a complete Any Science workspace:

```text
my-any-science/
├── CLAUDE.md                       # PI agent instructions
├── PROTOCOL.md                     # File contracts, state machine, errors
├── README.md                       # Generated workspace guide
├── .claude/
│   ├── agents/                     # builder, scholar, methodologist, executor, analyst, reviewer
│   ├── commands/                   # /build, /status, /spawn
│   ├── settings.json               # permissions and hooks
│   └── skills/                     # bootstrap, scientific-method, agent-factory, review-rubric
├── domain/
│   ├── PROFILE.md                  # Domain profile, initially TODO
│   ├── skills/                     # Domain-specific skills
│   └── references/                 # Domain references
├── scripts/
│   ├── validate.sh                 # Protocol validator
│   ├── review_gate.sh              # PostToolUse review gate
│   ├── pending_check.sh            # Stop hook check
│   ├── safe_kill.sh                # Safe long-task termination
│   ├── fork.sh                     # Long-running parallel research lines
│   └── harvest.sh                  # Knowledge harvesting from parallel lines
├── templates/
│   ├── idea_card.md
│   └── exp_card.md
└── workspace/
    ├── ideas/
    ├── experiments/
    └── knowledge/
```

## Optional Extensions: UI And Voice

Two optional extension installers are available under `dist/extensions/`. Windows users can install and run them natively from PowerShell without starting WSL. Bash remains available for Linux, macOS, and WSL.

### Local UI

Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  'D:\fu_files\工作\其他\any-science-framework-dev\dist\extensions\setup_ui.ps1' `
  -WorkspacePath 'D:\fu_files\工作\其他\any-science-workspace'
powershell -NoProfile -ExecutionPolicy Bypass -File `
  'D:\fu_files\工作\其他\any-science-workspace\scripts\ui_start.ps1'
```

Bash alternative:

```bash
cd /path/to/my-any-science
bash /path/to/any-science-framework/dist/extensions/setup_ui.sh
bash scripts/ui_start.sh
```

Then open:

```text
http://127.0.0.1:8321
```

UI boundaries:

- The UI is a read-only projection of `workspace/`.
- The only write entrypoint is `/api/inbox`, which writes to `workspace/inbox/`.
- UI input is semi-trusted and still goes through the PI workflow.
- The server binds only to `127.0.0.1` and keeps Host / Origin validation.
- Existing target files are backed up as `.bak.<timestamp>` before replacement.

Stop the UI:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  'D:\fu_files\工作\其他\any-science-workspace\scripts\ui_stop.ps1'
```

```bash
bash scripts/ui_stop.sh
```

### Voice Extension

Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  'D:\fu_files\工作\其他\any-science-framework-dev\dist\extensions\setup_voice.ps1' `
  -WorkspacePath 'D:\fu_files\工作\其他\any-science-workspace'
powershell -NoProfile -ExecutionPolicy Bypass -File `
  'D:\fu_files\工作\其他\any-science-workspace\scripts\voice\voice_status.ps1'
```

The native Windows path uses FFmpeg DirectShow, Windows SAPI, and an existing local Whisper `.pt` file. It enables offline flags and fails if the selected model is missing.

Bash alternative:

```bash
cd /path/to/my-any-science
bash /path/to/any-science-framework/dist/extensions/setup_voice.sh
bash scripts/voice/voice_status.sh
```

The voice extension does not fetch models or install dependencies. It only reuses local tools:

- Recording: `rec`, `arecord`, or `ffmpeg`
- STT: a local executable adapter, or `whisper-cli`, `whisper`, or `faster-whisper` with a complete local model
- TTS: `say`, `espeak-ng`, `espeak`, or WSL `powershell.exe` SAPI

Voice input example:

```bash
bash scripts/voice/dictate.sh 8
```

If you already have a local executable STT adapter, configure its path explicitly:

```bash
export ANY_SCIENCE_STT_ADAPTER=/absolute/path/to/local-stt-adapter
bash scripts/voice/dictate.sh 8
```

The transcript is shown for confirmation first. Once confirmed, it is written only to `workspace/inbox/` and does not directly edit ideas, experiments, or results.

## Recommended Workflow

### Domain Specialization

1. Enter the generated workspace.
2. Start `claude`.
3. Run `/build`.
4. Provide your field, research mode, resources, target venue, and reviewer red lines.
5. The builder fills `domain/PROFILE.md`, domain skills, and reviewer custom checks.

### From Idea To Experiment

1. Ask scholar to investigate literature, test novelty, or generate ideas.
2. Store idea cards under `workspace/ideas/IDEA-<id>.md`.
3. Run L1 or L2 review.
4. The PI agent transcribes reviewer findings and updates the final `REVIEW:` line.
5. Methodologist turns approved ideas into experiment cards.

### Execution And Analysis

1. Executor follows the experiment card without changing success criteria.
2. Results go under `workspace/experiments/<id>/results/`.
3. `metrics.json` must satisfy the schema in `PROTOCOL.md`.
4. Analyst runs `bash scripts/validate.sh` before analysis.
5. Analyst recommends `ITERATE`, `PROMOTE`, or `KILL` based on the pre-registered criteria.

### Status Checks

```bash
bash scripts/validate.sh
```

or inside Claude:

```text
/status
```

## Developing The Framework

Use this repository when changing the framework itself rather than using a generated research workspace.

### Build Release Scripts

```bash
bash scripts/build.sh
```

The build script copies `src/` into `dist/`, normalizes LF line endings, sets executable bits, and runs syntax checks.

### Run End-To-End Tests

```bash
bash tests/smoke.sh
```

The smoke test builds the release scripts, generates a temporary workspace, runs the generated validator, generates the test kit, and confirms that known bad cases are rejected.

## Safety Notes

Any Science Framework configures `.claude/settings.json`, hooks, and validators to reduce accidental mistakes. These are application-level soft constraints:

- They help prevent accidental secret reads, destructive commands, and skipped review.
- They do not defend against malicious code or deliberate bypass.
- They do not replace Docker, devcontainers, virtual machines, or OS-level isolation.

If you run untrusted code or handle sensitive data, use a sandboxed environment.

## FAQ

### Why does a new workspace refuse to do research at first?

Because `domain/PROFILE.md` starts in an unspecialized state. The framework requires `/build` first so that domain, evidence type, resources, venue standards, and review criteria are explicit.

### What is the difference between `src/` and `dist/`?

`src/` is the editable source version. `dist/` is the directly usable release version. After changing `src/`, run:

```bash
bash scripts/build.sh
```

### Can it overwrite an existing project?

Yes, if you point `dist/setup.sh` at an existing directory. Always generate into a new empty directory unless you intentionally want to merge files.

### Why enforce LF line endings?

The framework is run through Bash. Windows CRLF can make WSL/bash treat `\r` as command content, causing errors like `set: -\r: invalid option`. `.gitattributes` and the build script both enforce LF.

## Roadmap

- Split the current single-file `setup.sh` into template directories such as `templates/agents/`, `templates/scripts/`, and `templates/skills/`.
- Add more tests for hook input schema drift.
- Add release packaging and versioning.
- Add Docker/devcontainer examples.
- Add domain presets for AI/ML, biomedicine, materials, social science, and theoretical research.

## License

No license has been selected yet. Add an explicit open-source license before public reuse.
