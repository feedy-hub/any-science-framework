# VOICE_SPEC.md - Any Science Voice Extension

## Architecture Rules

1. Voice input writes only to `workspace/inbox/` with `source: voice`.
2. Voice input is semi-trusted and must be inspected with E-SEC-01.
3. Voice must never edit cards, results, knowledge files, or run experiments.
4. The Windows adapters use PowerShell argument arrays and never evaluate command strings.
5. Installers never fetch packages or models. STT uses only complete local model files.

## Windows Backends

- Recording: local FFmpeg with DirectShow.
- STT: an explicit local adapter, or OpenAI Whisper CLI with an existing `.pt` model.
- TTS: Windows `System.Speech` SAPI.

Environment overrides are paths or values, not shell command strings:

- `ANY_SCIENCE_FFMPEG`
- `ANY_SCIENCE_AUDIO_DEVICE`
- `ANY_SCIENCE_STT_ADAPTER`
- `ANY_SCIENCE_WHISPER_EXE`
- `ANY_SCIENCE_WHISPER_MODEL`
- `ANY_SCIENCE_WHISPER_CACHE`

All transcription subprocesses run with Python UTF-8 and Hugging Face offline flags.
