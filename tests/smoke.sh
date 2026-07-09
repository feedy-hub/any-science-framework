#!/bin/bash
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

bash "$ROOT/scripts/build.sh" >/tmp/anyscience_build.log
bash -n "$ROOT/dist/setup.sh"
bash -n "$ROOT/dist/setup_test.sh"

TMP_ROOT=$(mktemp -d /tmp/anyscience-framework-smoke.XXXXXX)
PROJECT="$TMP_ROOT/project"

bash "$ROOT/dist/setup.sh" "$PROJECT" >/tmp/anyscience_setup.log
cd "$PROJECT"

bash scripts/validate.sh >/tmp/anyscience_validate_ok.log

bash "$ROOT/dist/setup_test.sh" >/tmp/anyscience_testkit.log
test -f test-kit/TESTS.md
ls ../ANSWER_KEY_anyscience_*.md >/dev/null

printf '# IDEA-X\n- status: IDEA\n' > workspace/ideas/IDEA-X.md
if bash scripts/validate.sh >/tmp/anyscience_missing_review.log 2>&1; then
  echo "FAIL: missing REVIEW was not rejected"
  exit 1
fi
grep -q '\[E03\]' /tmp/anyscience_missing_review.log
rm -f workspace/ideas/IDEA-X.md

printf '# IDEA-DUP\n- status: IDEA\n- status: DESIGN\n\nREVIEW: PENDING\n' > workspace/ideas/IDEA-DUP.md
if bash scripts/validate.sh >/tmp/anyscience_dup_status.log 2>&1; then
  echo "FAIL: duplicate status was not rejected"
  exit 1
fi
grep -q '\[E02\]' /tmp/anyscience_dup_status.log
rm -f workspace/ideas/IDEA-DUP.md

mkdir -p workspace/experiments/EXP-BAD/results
printf '# EXP-BAD\n- status: DESIGN\n\nREVIEW: PENDING\nextra\n' > workspace/experiments/EXP-BAD/card.md
printf '{"_meta":{"date":"2026-07-07"}}' > workspace/experiments/EXP-BAD/results/metrics.json
if bash scripts/validate.sh >/tmp/anyscience_bad_exp.log 2>&1; then
  echo "FAIL: bad experiment card/metrics were not rejected"
  exit 1
fi
grep -q '\[E03\]' /tmp/anyscience_bad_exp.log
grep -q '\[E06\]' /tmp/anyscience_bad_exp.log

cd "$ROOT"
rm -rf "$TMP_ROOT"
"$ROOT/tests/extensions_smoke.sh"
echo "OK: smoke tests passed"
