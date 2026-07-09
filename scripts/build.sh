#!/bin/bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
mkdir -p "$ROOT/dist"
mkdir -p "$ROOT/dist/extensions"

copy_release() {
  local src=$1
  local dst=$2
  python3 - "$src" "$dst" << 'PY'
from pathlib import Path
import sys
src = Path(sys.argv[1])
dst = Path(sys.argv[2])
text = src.read_text(encoding="utf-8")
text = text.replace("\r\n", "\n").replace("\r", "\n")
dst.write_text(text, encoding="utf-8", newline="\n")
PY
  chmod +x "$dst"
}

copy_release "$ROOT/src/setup.sh" "$ROOT/dist/setup.sh"
copy_release "$ROOT/src/setup_test.sh" "$ROOT/dist/setup_test.sh"
for ext in "$ROOT"/src/extensions/*.sh; do
  [ -f "$ext" ] || continue
  copy_release "$ext" "$ROOT/dist/extensions/$(basename "$ext")"
done

bash -n "$ROOT/dist/setup.sh"
bash -n "$ROOT/dist/setup_test.sh"
for ext in "$ROOT"/dist/extensions/*.sh; do
  [ -f "$ext" ] && bash -n "$ext"
done

echo "OK: release scripts built in dist/"
