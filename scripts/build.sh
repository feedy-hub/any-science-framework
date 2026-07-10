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
for ext in "$ROOT"/src/extensions/*.ps1; do
  [ -f "$ext" ] || continue
  copy_release "$ext" "$ROOT/dist/extensions/$(basename "$ext")"
done

if [ -d "$ROOT/src/extensions/assets" ]; then
  while IFS= read -r -d '' asset; do
    rel=${asset#"$ROOT/src/extensions/assets/"}
    copy_release "$asset" "$ROOT/dist/extensions/assets/$rel"
  done < <(find "$ROOT/src/extensions/assets" -type f -print0)
fi

bash -n "$ROOT/dist/setup.sh"
bash -n "$ROOT/dist/setup_test.sh"
for ext in "$ROOT"/dist/extensions/*.sh; do
  [ -f "$ext" ] && bash -n "$ext"
done

echo "OK: release scripts built in dist/"
