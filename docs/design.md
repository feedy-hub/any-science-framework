# Any Science Framework Dev Design

## Goal

Turn the working v1.5 desktop scripts into a reusable development package with repeatable build and verification.

## First Usable Slice

The first version keeps the existing scripts as the source of truth, adds a project layout, and introduces automated checks that prevent the previous failures from returning:

- LF-only release scripts.
- `bash -n` syntax checks.
- End-to-end project generation.
- `validate.sh` positive and negative checks.
- test-kit generation with non-overwriting answer keys.

## Architecture

`src/` contains editable source scripts. `scripts/build.sh` copies them into `dist/`, normalizes line endings, and marks them executable. `tests/smoke.sh` builds a fresh release into a temporary directory, generates an Any Science project, runs validation, generates the test kit, and checks known bad cases.

This keeps the current framework usable immediately while creating a path to split the large setup script into smaller templates later.

## Non-Goals For This Slice

- No UI.
- No package manager installer.
- No rewrite of the generated Claude agents.
- No deep templating engine yet.

