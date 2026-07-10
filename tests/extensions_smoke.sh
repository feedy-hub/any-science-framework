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

UI_TEST_PORT=18322
ANY_SCIENCE_UI_PORT=$UI_TEST_PORT python3 ui/server.py >/tmp/anyscience_ui_server.log 2>&1 &
UI_TEST_PID=$!
cleanup_ui_test() {
  kill "$UI_TEST_PID" >/dev/null 2>&1 || true
}
trap cleanup_ui_test EXIT
python3 - "$UI_TEST_PORT" <<'PY'
import json
import sys
import time
import urllib.error
import urllib.request

port = int(sys.argv[1])
base = f"http://127.0.0.1:{port}"
for _ in range(50):
    try:
        urllib.request.urlopen(base + "/api/overview", timeout=1).read()
        break
    except OSError:
        time.sleep(0.1)
else:
    raise AssertionError("UI server did not become ready")

request = urllib.request.Request(
    base + "/api/inbox",
    data=json.dumps([]).encode("utf-8"),
    headers={"Content-Type": "application/json"},
    method="POST",
)
try:
    urllib.request.urlopen(request, timeout=2)
except urllib.error.HTTPError as exc:
    assert exc.code == 400, exc.code
else:
    raise AssertionError("array JSON payload was not rejected")
PY
cleanup_ui_test
trap - EXIT

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
if grep -R "ANY_SCIENCE_WHISPER_CMD" scripts/voice VOICE_SPEC.md; then
  echo "FAIL: voice extension retained unsafe command-string execution"
  exit 1
fi
grep -q 'HF_HUB_OFFLINE=1' scripts/voice/stt.sh
grep -q 'TRANSFORMERS_OFFLINE=1' scripts/voice/stt.sh
grep -q 'No complete local Whisper model' scripts/voice/stt.sh

cd "$ROOT"
rm -rf "$TMP_ROOT"
echo "OK: extension smoke tests passed"
