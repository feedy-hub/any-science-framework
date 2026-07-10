# UI_SPEC.md - Any Science UI Extension

## Architecture Rules

1. The UI is a read-only projection of the workspace. It must not write cards, results, or knowledge files.
2. The only write API is `POST /api/inbox`, which writes semi-trusted requests into `workspace/inbox/`.
3. The backend uses only the Python standard library. The frontend is local and has no CDN or npm build step.
4. The server binds only to `127.0.0.1`.
5. Invalid cards are shown with protocol error codes instead of being silently skipped.

## Safety Checklist

- Keep `os.path.commonpath` checks for path parameters.
- Keep Host validation on all requests and Origin validation on writes.
- Keep request body limits and JSON type validation.
- Keep all UI-originated requests semi-trusted and subject to E-SEC-01.
