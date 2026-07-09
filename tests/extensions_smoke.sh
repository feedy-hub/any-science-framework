#!/bin/bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

bash "$ROOT/scripts/build.sh" >/tmp/anyscience_ext_build.log

for script in "$ROOT/dist/extensions/setup_ui.sh" "$ROOT/dist/extensions/setup_voice.sh"; do
  test -f "$script"
  bash -n "$script"
  if grep -q $'\r' "$script"; then
    echo "FAIL: CRLF detected in $script"
    exit 1
  fi
done

TMP_ROOT=$(mktemp -d /tmp/anyscience-ext-smoke.XXXXXX)
PROJECT="$TMP_ROOT/project"

bash "$ROOT/dist/setup.sh" "$PROJECT" >/tmp/anyscience_ext_setup.log
cd "$PROJECT"

bash "$ROOT/dist/extensions/setup_ui.sh" >/tmp/anyscience_ui_install.log
python3 -m py_compile ui/server.py
bash -n scripts/ui_start.sh
bash -n scripts/ui_stop.sh
bash scripts/validate.sh >/tmp/anyscience_ui_validate.log

python3 - <<'PY'
from pathlib import Path
server = Path("ui/server.py").read_text(encoding="utf-8")
assert "os.path.commonpath" in server
assert "APPROVED\", \"RUNNING\", \"ANALYZED\", \"PROMOTE\"" in server
assert "ThreadingHTTPServer((\"127.0.0.1\"" in server
assert "0.0.0.0" not in server
PY

bash "$ROOT/dist/extensions/setup_voice.sh" >/tmp/anyscience_voice_install.log
for script in scripts/voice/say.sh scripts/voice/stt.sh scripts/voice/dictate.sh scripts/voice/voice_status.sh; do
  test -f "$script"
  bash -n "$script"
done
bash scripts/voice/voice_status.sh >/tmp/anyscience_voice_status.log
bash scripts/validate.sh >/tmp/anyscience_voice_validate.log

if grep -R "pip install\|curl \|wget \|git clone\|model.*download\|download.*model" scripts/voice VOICE_SPEC.md; then
  echo "FAIL: voice extension attempted or instructed automatic model downloads"
  exit 1
fi

cd "$ROOT"
rm -rf "$TMP_ROOT"
echo "OK: extension smoke tests passed"
