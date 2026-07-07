# Any Science Framework Dev Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a maintainable release package around the existing Any Science setup scripts.

**Architecture:** Keep source scripts in `src/`, generate release scripts into `dist/`, and verify through `tests/smoke.sh`.

**Tech Stack:** Bash, Git, PowerShell only for local Windows file handling when needed.

---

### Task 1: Project Skeleton

**Files:**
- Create: `README.md`
- Create: `docs/design.md`
- Create: `docs/plan.md`
- Create: `.gitignore`

- [x] Create the project layout and documentation.

### Task 2: Source And Build

**Files:**
- Create: `src/setup.sh`
- Create: `src/setup_test.sh`
- Create: `scripts/build.sh`
- Create: `dist/setup.sh`
- Create: `dist/setup_test.sh`

- [ ] Copy the current v1.5 scripts into `src/`.
- [ ] Add a build script that normalizes LF, copies to `dist/`, and sets executable bits.
- [ ] Run the build script and verify `dist/` scripts exist.

### Task 3: Smoke Tests

**Files:**
- Create: `tests/smoke.sh`

- [ ] Add syntax checks for both release scripts.
- [ ] Generate a temporary Any Science project.
- [ ] Run the generated `scripts/validate.sh`.
- [ ] Generate `test-kit/` with `dist/setup_test.sh`.
- [ ] Check negative validation cases for missing REVIEW, duplicated status, REVIEW not at EOF, and invalid metrics.

### Task 4: Verification

**Files:**
- Modify: `README.md`

- [ ] Run `bash scripts/build.sh`.
- [ ] Run `bash tests/smoke.sh`.
- [ ] Document release usage in README.

