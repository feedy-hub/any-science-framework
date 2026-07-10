[CmdletBinding()]
param(
    [ValidateRange(1, 120)]
    [int]$Seconds = 8,
    [string]$AudioDevice = $env:ANY_SCIENCE_AUDIO_DEVICE,
    [string]$AudioFile,
    [string]$AdapterPath = $env:ANY_SCIENCE_STT_ADAPTER,
    [string]$WhisperExecutable = $env:ANY_SCIENCE_WHISPER_EXE,
    [string]$Model = $env:ANY_SCIENCE_WHISPER_MODEL,
    [string]$ModelCachePath = $(if ($env:ANY_SCIENCE_WHISPER_CACHE) { $env:ANY_SCIENCE_WHISPER_CACHE } else { Join-Path $HOME '.cache\whisper' }),
    [switch]$AutoConfirm
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$temporaryAudio = $false

function Find-Ffmpeg {
    if ($env:ANY_SCIENCE_FFMPEG -and (Test-Path -LiteralPath $env:ANY_SCIENCE_FFMPEG -PathType Leaf)) { return $env:ANY_SCIENCE_FFMPEG }
    $command = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command ffmpeg -ErrorAction SilentlyContinue }
    if ($command) { return $command.Source }
    throw 'FFmpeg was not found. Set ANY_SCIENCE_FFMPEG to an existing ffmpeg.exe path.'
}

function Get-AudioDevices {
    param([Parameter(Mandatory = $true)][string]$Ffmpeg)
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $lines = & $Ffmpeg -hide_banner -list_devices true -f dshow -i dummy 2>&1 } finally { $ErrorActionPreference = $oldPreference }
    $devices = @()
    foreach ($line in $lines) {
        if ([string]$line -match '"(?<name>.+)" \(audio\)') { $devices += $Matches.name }
    }
    return @($devices | Select-Object -Unique)
}

try {
    if ($AudioFile) {
        $AudioFile = (Resolve-Path -LiteralPath $AudioFile).Path
    }
    else {
        $ffmpeg = Find-Ffmpeg
        $devices = @(Get-AudioDevices -Ffmpeg $ffmpeg)
        if (-not $AudioDevice) {
            if ($devices.Count -eq 1) { $AudioDevice = $devices[0] }
            elseif ($devices.Count -eq 0) { throw 'No Windows DirectShow microphone was found.' }
            else { throw "Multiple microphones found. Set ANY_SCIENCE_AUDIO_DEVICE to one of: $($devices -join '; ')" }
        }
        if ($devices.Count -gt 0 -and $AudioDevice -notin $devices) {
            throw "Audio device was not found: $AudioDevice"
        }
        $AudioFile = Join-Path ([IO.Path]::GetTempPath()) ('anyscience-voice-' + [guid]::NewGuid().ToString('N') + '.wav')
        $temporaryAudio = $true
        Write-Host "Recording ${Seconds}s from: $AudioDevice"
        $arguments = @('-hide_banner', '-loglevel', 'error', '-f', 'dshow', '-i', "audio=$AudioDevice", '-t', [string]$Seconds, '-ac', '1', '-ar', '16000', '-y', $AudioFile)
        & $ffmpeg @arguments
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $AudioFile -PathType Leaf)) {
            throw "FFmpeg recording failed with code $LASTEXITCODE"
        }
    }

    $parameters = @{ AudioFile = $AudioFile; ModelCachePath = $ModelCachePath }
    if ($AdapterPath) { $parameters.AdapterPath = $AdapterPath }
    if ($WhisperExecutable) { $parameters.WhisperExecutable = $WhisperExecutable }
    if ($Model) { $parameters.Model = $Model }
    $output = & (Join-Path $PSScriptRoot 'stt.ps1') @parameters
    $text = [regex]::Replace(($output -join ' '), '\s+', ' ').Trim()
    if (-not $text) { throw 'Transcription is empty' }
    Write-Host "Transcription: $text"

    if (-not $AutoConfirm) {
        $answer = Read-Host 'Write to workspace/inbox? [Y/n]'
        if ($answer -match '^[Nn]') { return }
    }

    $inbox = Join-Path $Root 'workspace\inbox'
    [IO.Directory]::CreateDirectory($inbox) | Out-Null
    $path = Join-Path $inbox "voice-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$PID.md"
    $content = "# Voice Request $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n- status: pending`n- source: voice`n- trust: semi-trusted; inspect with E-SEC-01`n`n$text`n"
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
    Write-Host "Saved: $path"
}
finally {
    if ($temporaryAudio -and $AudioFile -and (Test-Path -LiteralPath $AudioFile)) {
        Remove-Item -LiteralPath $AudioFile -Force
    }
}
