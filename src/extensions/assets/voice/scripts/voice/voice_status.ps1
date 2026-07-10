[CmdletBinding()]
param(
    [switch]$AsJson,
    [string]$ModelCachePath = $(if ($env:ANY_SCIENCE_WHISPER_CACHE) { $env:ANY_SCIENCE_WHISPER_CACHE } else { Join-Path $HOME '.cache\whisper' })
)

$ErrorActionPreference = 'Stop'
$ffmpeg = $null
if ($env:ANY_SCIENCE_FFMPEG -and (Test-Path -LiteralPath $env:ANY_SCIENCE_FFMPEG -PathType Leaf)) { $ffmpeg = $env:ANY_SCIENCE_FFMPEG }
if (-not $ffmpeg) {
    $command = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command ffmpeg -ErrorAction SilentlyContinue }
    if ($command) { $ffmpeg = $command.Source }
}

$devices = @()
if ($ffmpeg) {
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $lines = & $ffmpeg -hide_banner -list_devices true -f dshow -i dummy 2>&1 } finally { $ErrorActionPreference = $oldPreference }
    foreach ($line in $lines) {
        if ([string]$line -match '"(?<name>.+)" \(audio\)') { $devices += $Matches.name }
    }
    $devices = @($devices | Select-Object -Unique)
}

$whisper = $env:ANY_SCIENCE_WHISPER_EXE
if (-not $whisper) {
    $command = Get-Command whisper.exe -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command whisper -ErrorAction SilentlyContinue }
    if ($command) { $whisper = $command.Source }
}
$models = @()
if (Test-Path -LiteralPath $ModelCachePath -PathType Container) {
    $models = @(Get-ChildItem -LiteralPath $ModelCachePath -File -Filter '*.pt' | Sort-Object Name | ForEach-Object { $_.FullName })
}

$status = [ordered]@{
    downloads_allowed = $false
    ffmpeg = $ffmpeg
    audio_devices = $devices
    whisper = $whisper
    cached_models = $models
    selected_audio_device = $env:ANY_SCIENCE_AUDIO_DEVICE
    selected_model = $env:ANY_SCIENCE_WHISPER_MODEL
    adapter = $env:ANY_SCIENCE_STT_ADAPTER
}
if ($AsJson) {
    $status | ConvertTo-Json -Depth 4 -Compress
    return
}

Write-Host 'Any Science Voice status (downloads disabled)'
Write-Host "FFmpeg: $(if ($ffmpeg) { $ffmpeg } else { '-- not found' })"
Write-Host "Microphones: $(if ($devices.Count) { $devices -join '; ' } else { '-- none found' })"
Write-Host "Whisper: $(if ($whisper) { $whisper } else { '-- not found' })"
Write-Host "Cached models: $(if ($models.Count) { $models -join '; ' } else { '-- none found' })"
Write-Host "STT adapter: $(if ($env:ANY_SCIENCE_STT_ADAPTER) { $env:ANY_SCIENCE_STT_ADAPTER } else { '-- not configured' })"
