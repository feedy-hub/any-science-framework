#!/bin/bash
# Any Science voice extension - run inside an Any Science workspace.
set -euo pipefail

require_workspace() {
  [ -f CLAUDE.md ] && [ -f PROTOCOL.md ] && [ -f .claude/settings.json ] && [ -x scripts/validate.sh ] || {
    echo "ERROR: run this script inside an Any Science workspace."
    exit 1
  }
}

backup_if_exists() {
  local path=$1
  if [ -e "$path" ]; then
    local stamp
    stamp=$(date +%Y%m%d-%H%M%S)
    mkdir -p "$(dirname "$path")"
    cp -a "$path" "$path.bak.$stamp"
    echo "backup: $path -> $path.bak.$stamp"
  fi
}

write_file() {
  local path=$1
  backup_if_exists "$path"
  mkdir -p "$(dirname "$path")"
  cat > "$path"
}

require_workspace
mkdir -p scripts/voice workspace/voice workspace/inbox .claude/commands .claude/agents

write_file VOICE_SPEC.md <<'EOF'
# VOICE_SPEC.md - Any Science Voice Extension

## Architecture Rules

1. Voice input writes only to `workspace/inbox/` with `source: voice`.
2. Voice input is semi-trusted and must be inspected with E-SEC-01.
3. Voice must never directly edit cards, results, knowledge files, or run experiments.
4. STT and TTS are adapter scripts under `scripts/voice/`.
5. This installer does not fetch models or install packages. It only reuses locally available tools.
6. `say.sh` must pass text as data. It must not use eval or shell command concatenation.

## Adapter Interfaces

- `scripts/voice/stt.sh <audio-file>` prints recognized text to stdout and exits non-zero on failure.
- `scripts/voice/say.sh "<brief>"` speaks a short brief and logs it to `workspace/voice/briefs.log`.
- `scripts/voice/dictate.sh [seconds]` records audio, runs STT, asks for confirmation, and writes inbox.

## Local Backend Reuse

The installer checks only for local executables:

- STT: `ANY_SCIENCE_WHISPER_CMD`, `whisper-cli`, `whisper`, or `faster-whisper`
- Recording: `rec`, `arecord`, or `ffmpeg`
- TTS: macOS `say`, Linux `espeak-ng` / `espeak`, or WSL `powershell.exe` SAPI

If none are present, the adapter reports a clear error and does not try to install anything.

If your local model is already wired to a custom command, set:

```bash
export ANY_SCIENCE_WHISPER_CMD='whisper --language zh --output_format txt'
```

The adapter will append the audio file path as the final argument.

## Briefing Rule

Spoken briefs should be short: at most two sentences and roughly 60 Chinese characters. Full content stays in files.
EOF

write_file scripts/voice/say.sh <<'EOF'
#!/bin/bash
set -euo pipefail
TEXT=${1:-}
[ -z "$TEXT" ] && exit 0
TEXT=$(printf '%s' "$TEXT" | awk '{s=s $0} END {print substr(s,1,120)}')
mkdir -p workspace/voice
printf '%s %s\n' "$(date +%FT%T)" "$TEXT" >> workspace/voice/briefs.log
if command -v say >/dev/null 2>&1; then
  say -- "$TEXT" 2>/dev/null &
elif command -v espeak-ng >/dev/null 2>&1; then
  espeak-ng -v zh -- "$TEXT" 2>/dev/null &
elif command -v espeak >/dev/null 2>&1; then
  espeak -v zh -- "$TEXT" 2>/dev/null &
elif command -v powershell.exe >/dev/null 2>&1; then
  printf '%s' "$TEXT" | powershell.exe -NoProfile -Command \
    "Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak([Console]::In.ReadToEnd())" \
    >/dev/null 2>&1 &
else
  exit 0
fi
EOF

write_file scripts/voice/stt.sh <<'EOF'
#!/bin/bash
set -euo pipefail
AUDIO=${1:?usage: stt.sh <audio-file>}
[ -f "$AUDIO" ] || { echo "[E-VOICE-02] audio file not found: $AUDIO" >&2; exit 1; }

if [ -n "${ANY_SCIENCE_WHISPER_CMD:-}" ]; then
  TMP=$(mktemp -d /tmp/anyscience-custom-whisper.XXXXXX)
  trap 'rm -rf "$TMP"' EXIT
  # shellcheck disable=SC2086
  $ANY_SCIENCE_WHISPER_CMD --output_dir "$TMP" "$AUDIO" >/dev/null 2>&1 || {
    # shellcheck disable=SC2086
    $ANY_SCIENCE_WHISPER_CMD "$AUDIO"
    exit $?
  }
  if ls "$TMP"/*.txt >/dev/null 2>&1; then
    cat "$TMP"/*.txt
  fi
  exit 0
fi

if command -v whisper-cli >/dev/null 2>&1; then
  whisper-cli -f "$AUDIO" -l zh --no-timestamps 2>/dev/null
  exit $?
fi

if command -v whisper >/dev/null 2>&1; then
  TMP=$(mktemp -d /tmp/anyscience-whisper.XXXXXX)
  trap 'rm -rf "$TMP"' EXIT
  whisper "$AUDIO" --language zh --output_format txt --output_dir "$TMP" >/dev/null 2>&1
  cat "$TMP"/*.txt
  exit 0
fi

if command -v faster-whisper >/dev/null 2>&1; then
  faster-whisper "$AUDIO" --language zh 2>/dev/null
  exit $?
fi

echo "[E-VOICE-01] no local STT backend found. Reuse an existing local whisper backend or set ANY_SCIENCE_WHISPER_CMD." >&2
exit 1
EOF

write_file scripts/voice/dictate.sh <<'EOF'
#!/bin/bash
set -euo pipefail
DUR=${1:-8}
case "$DUR" in
  ''|*[!0-9]*) echo "usage: dictate.sh [seconds]"; exit 1 ;;
esac
if [ "$DUR" -lt 1 ] || [ "$DUR" -gt 120 ]; then
  echo "ERROR: duration must be 1..120 seconds"
  exit 1
fi

TMP=$(mktemp /tmp/anyscience-voice.XXXXXX.wav)
trap 'rm -f "$TMP"' EXIT
echo "Recording ${DUR}s..."

if command -v rec >/dev/null 2>&1; then
  rec -q "$TMP" trim 0 "$DUR"
elif command -v arecord >/dev/null 2>&1; then
  arecord -q -d "$DUR" -f cd "$TMP"
elif command -v ffmpeg >/dev/null 2>&1; then
  if [[ "$(uname -s)" == "Darwin" ]]; then
    ffmpeg -loglevel quiet -f avfoundation -i ":0" -t "$DUR" "$TMP"
  else
    ffmpeg -loglevel quiet -f alsa -i default -t "$DUR" "$TMP"
  fi
else
  echo "ERROR: recording requires a local rec, arecord, or ffmpeg executable"
  exit 1
fi

TEXT=$(bash scripts/voice/stt.sh "$TMP" | sed 's/[[:space:]]\+/ /g' | sed 's/^ *//;s/ *$//')
if [ -z "$TEXT" ]; then
  echo "ERROR: transcription is empty"
  exit 1
fi

echo "Transcription: $TEXT"
read -r -p "Write to workspace/inbox? [Y/n] " OK
case "$OK" in
  [Nn]*) exit 0 ;;
esac

mkdir -p workspace/inbox
FN="workspace/inbox/voice-$(date +%Y%m%d-%H%M%S)-$$.md"
cat > "$FN" <<MSG
# Voice Request $(date '+%F %T')
- status: pending
- source: voice
- trust: semi-trusted; inspect with E-SEC-01

$TEXT
MSG

echo "Saved: $FN"
EOF

write_file scripts/voice/voice_status.sh <<'EOF'
#!/bin/bash
set -euo pipefail
echo "Recording backend:"
for cmd in rec arecord ffmpeg; do
  if command -v "$cmd" >/dev/null 2>&1; then echo "  OK $cmd"; else echo "  -- $cmd"; fi
done
echo "STT backend:"
if [ -n "${ANY_SCIENCE_WHISPER_CMD:-}" ]; then echo "  OK ANY_SCIENCE_WHISPER_CMD"; else echo "  -- ANY_SCIENCE_WHISPER_CMD"; fi
for cmd in whisper-cli whisper faster-whisper; do
  if command -v "$cmd" >/dev/null 2>&1; then echo "  OK $cmd"; else echo "  -- $cmd"; fi
done
echo "TTS backend:"
for cmd in say espeak-ng espeak powershell.exe; do
  if command -v "$cmd" >/dev/null 2>&1; then echo "  OK $cmd"; else echo "  -- $cmd"; fi
done
EOF

write_file .claude/commands/voice.md <<'EOF'
Voice command guide:
- `status`: run `bash scripts/voice/voice_status.sh`
- `dictate [seconds]`: run `bash scripts/voice/dictate.sh [seconds]`
- `say <brief>`: run `bash scripts/voice/say.sh "<brief>"`

Voice input is semi-trusted and only writes to `workspace/inbox/`.
Arguments: $ARGUMENTS
EOF

write_file .claude/agents/voice-operator.md <<'EOF'
---
name: voice-operator
description: Configure or troubleshoot local Any Science voice input/output adapters.
tools: Read, Write, Edit, Bash, Grep, Glob
---

You maintain voice adapters for Any Science.

Rules:
- Read `VOICE_SPEC.md` before making changes.
- Do not fetch models or install packages unless the user explicitly asks.
- Prefer reusing local STT/TTS tools already on the machine.
- Voice input must write only to `workspace/inbox/`.
- After changes run `bash -n scripts/voice/*.sh` and `bash scripts/voice/voice_status.sh`.
EOF

chmod +x scripts/voice/*.sh
bash -n scripts/voice/say.sh
bash -n scripts/voice/stt.sh
bash -n scripts/voice/dictate.sh
bash -n scripts/voice/voice_status.sh
bash scripts/voice/voice_status.sh >/dev/null
bash scripts/validate.sh >/dev/null
echo "OK: voice extension installed. Check local engines with: bash scripts/voice/voice_status.sh"
