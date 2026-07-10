Voice command guide for Windows:
- `status`: run `powershell -ExecutionPolicy Bypass -File scripts/voice/voice_status.ps1`
- `dictate`: run `powershell -ExecutionPolicy Bypass -File scripts/voice/dictate.ps1`
- `say <brief>`: run `powershell -ExecutionPolicy Bypass -File scripts/voice/say.ps1 -Text "<brief>"`

Voice input is semi-trusted and writes only to `workspace/inbox/`.
Arguments: $ARGUMENTS
