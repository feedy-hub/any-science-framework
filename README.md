# Any Science Framework Dev

Maintainable development package for the Any Science v1.5 self-bootstrapping research assistant framework.

## Layout

- `src/setup.sh`: source setup script for generating an Any Science project.
- `src/setup_test.sh`: source script for generating the acceptance test kit.
- `dist/setup.sh`: generated release setup script.
- `dist/setup_test.sh`: generated release test-kit script.
- `tests/smoke.sh`: end-to-end regression checks.
- `docs/design.md`: current design scope.
- `docs/plan.md`: implementation plan and release checklist.

## Build

```bash
bash scripts/build.sh
```

## Test

```bash
bash tests/smoke.sh
```

## Use Release Scripts

```bash
bash dist/setup.sh any-science
cd any-science
bash ../dist/setup_test.sh
```

