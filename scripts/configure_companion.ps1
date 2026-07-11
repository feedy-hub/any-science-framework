[CmdletBinding()]
param(
    [string]$FfmpegPath,
    [string]$WhisperPath,
    [string]$ModelPath,
    [string]$AudioDevice,
    [switch]$EnableHotkey
)

$ErrorActionPreference = 'Stop'

function Assert-ExistingFile {
    param([string]$Path, [string]$Label)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist: $Path"
    }

    return (Get-Item -LiteralPath $Path).FullName
}

$baseDirectory = Join-Path $env:LOCALAPPDATA 'AnyVoiceCompanion'
$configFile = Join-Path $baseDirectory 'config.json'
$values = [ordered]@{
    schemaVersion = 1
    scale = 1.0
    opacity = 1.0
    subtitlesEnabled = $true
    speechEnabled = $true
    hotkeyEnabled = $false
    startWithWindows = $false
    subtitleDurationSeconds = 6
    windowLeft = $null
    windowTop = $null
    ffmpegPath = $null
    whisperPath = $null
    whisperModelPath = $null
    audioDevice = $null
    retainDiagnosticAudio = $false
}

if (Test-Path -LiteralPath $configFile -PathType Leaf) {
    $existing = Get-Content -LiteralPath $configFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $existing.PSObject.Properties) {
        $values[$property.Name] = $property.Value
    }
}

if ($PSBoundParameters.ContainsKey('FfmpegPath')) {
    $values.ffmpegPath = Assert-ExistingFile $FfmpegPath 'FFmpeg'
}

if ($PSBoundParameters.ContainsKey('WhisperPath')) {
    $values.whisperPath = Assert-ExistingFile $WhisperPath 'Whisper'
}

if ($PSBoundParameters.ContainsKey('ModelPath')) {
    $model = Assert-ExistingFile $ModelPath 'Whisper model'
    if ([IO.Path]::GetExtension($model) -ne '.pt') {
        throw "Whisper model must be a .pt file: $model"
    }

    $values.whisperModelPath = $model
}

if ($PSBoundParameters.ContainsKey('AudioDevice')) {
    if ([string]::IsNullOrWhiteSpace($AudioDevice)) {
        throw 'AudioDevice cannot be empty.'
    }

    $values.audioDevice = $AudioDevice.Trim()
}

if ($EnableHotkey) {
    $values.hotkeyEnabled = $true
}

New-Item -ItemType Directory -Path $baseDirectory -Force | Out-Null
if (Test-Path -LiteralPath $configFile -PathType Leaf) {
    $backup = "$configFile.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $configFile -Destination $backup
}

$temporaryFile = "$configFile.tmp-$([guid]::NewGuid().ToString('N'))"
try {
    $json = $values | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($temporaryFile, $json, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryFile -Destination $configFile -Force
}
finally {
    Remove-Item -LiteralPath $temporaryFile -Force -ErrorAction SilentlyContinue
}

Write-Output $configFile
