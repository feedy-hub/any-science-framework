[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$AudioFile,
    [string]$AdapterPath = $env:ANY_SCIENCE_STT_ADAPTER,
    [string]$WhisperExecutable = $env:ANY_SCIENCE_WHISPER_EXE,
    [string]$Model = $env:ANY_SCIENCE_WHISPER_MODEL,
    [string]$ModelCachePath = $(if ($env:ANY_SCIENCE_WHISPER_CACHE) { $env:ANY_SCIENCE_WHISPER_CACHE } else { Join-Path $HOME '.cache\whisper' })
)

$ErrorActionPreference = 'Stop'
$AudioFile = (Resolve-Path -LiteralPath $AudioFile).Path

if ($AdapterPath) {
    $AdapterPath = (Resolve-Path -LiteralPath $AdapterPath).Path
    if ([IO.Path]::GetExtension($AdapterPath) -ieq '.ps1') {
        $output = & $AdapterPath -AudioFile $AudioFile
    }
    else {
        $output = & $AdapterPath $AudioFile
        if ($LASTEXITCODE -ne 0) { throw "STT adapter exited with code $LASTEXITCODE" }
    }
    $text = [regex]::Replace(($output -join ' '), '\s+', ' ').Trim()
    if (-not $text) { throw 'STT adapter returned an empty transcription' }
    Write-Output $text
    return
}

if (-not $WhisperExecutable) {
    $command = Get-Command whisper.exe -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command whisper -ErrorAction SilentlyContinue }
    if ($command) { $WhisperExecutable = $command.Source }
}
if (-not $WhisperExecutable -or -not (Test-Path -LiteralPath $WhisperExecutable -PathType Leaf)) {
    throw 'No local Whisper executable was found. Set ANY_SCIENCE_WHISPER_EXE to an existing executable path.'
}

$modelPath = $null
if ($Model) {
    if (Test-Path -LiteralPath $Model -PathType Leaf) {
        $modelPath = (Resolve-Path -LiteralPath $Model).Path
    }
    else {
        $name = if ($Model.EndsWith('.pt')) { $Model } else { "$Model.pt" }
        $candidate = Join-Path $ModelCachePath $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $modelPath = (Resolve-Path -LiteralPath $candidate).Path }
    }
}
else {
    foreach ($name in @('turbo.pt', 'large-v3.pt', 'large-v2.pt', 'large.pt', 'medium.pt', 'small.pt', 'base.pt', 'tiny.pt')) {
        $candidate = Join-Path $ModelCachePath $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $modelPath = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
    }
}
if (-not $modelPath) {
    throw "No complete local Whisper model exists in model cache: $ModelCachePath"
}

$outputDirectory = Join-Path ([IO.Path]::GetTempPath()) ('anyscience-stt-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$oldEnvironment = @{
    PYTHONUTF8 = $env:PYTHONUTF8
    PYTHONIOENCODING = $env:PYTHONIOENCODING
    HF_HUB_OFFLINE = $env:HF_HUB_OFFLINE
    TRANSFORMERS_OFFLINE = $env:TRANSFORMERS_OFFLINE
}
try {
    $env:PYTHONUTF8 = '1'
    $env:PYTHONIOENCODING = 'utf-8'
    $env:HF_HUB_OFFLINE = '1'
    $env:TRANSFORMERS_OFFLINE = '1'
    $arguments = @(
        $AudioFile,
        '--model', $modelPath,
        '--language', 'zh',
        '--output_format', 'txt',
        '--output_dir', $outputDirectory,
        '--verbose', 'False'
    )
    & $WhisperExecutable @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Whisper exited with code $LASTEXITCODE" }
    $transcript = Get-ChildItem -LiteralPath $outputDirectory -File -Filter '*.txt' | Select-Object -First 1
    if (-not $transcript) { throw 'Whisper did not create a transcript file' }
    $text = [regex]::Replace([IO.File]::ReadAllText($transcript.FullName, [Text.Encoding]::UTF8), '\s+', ' ').Trim()
    if (-not $text) { throw 'Whisper returned an empty transcription' }
    Write-Output $text
}
finally {
    foreach ($name in $oldEnvironment.Keys) {
        $value = $oldEnvironment[$name]
        if ($null -eq $value) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue } else { Set-Item "Env:$name" $value }
    }
    if (Test-Path -LiteralPath $outputDirectory) { Remove-Item -LiteralPath $outputDirectory -Recurse -Force }
}
