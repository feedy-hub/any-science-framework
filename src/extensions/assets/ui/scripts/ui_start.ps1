[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = $(if ($env:ANY_SCIENCE_UI_PORT) { [int]$env:ANY_SCIENCE_UI_PORT } else { 8321 }),
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PidFile = Join-Path $Root '.claude\ui.pid'
$LogFile = Join-Path $Root '.claude\ui.log'
$ErrorLog = Join-Path $Root '.claude\ui.error.log'
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

function Find-Python {
    $candidates = @()
    if ($env:CONDA_PREFIX) { $candidates += (Join-Path $env:CONDA_PREFIX 'python.exe') }
    $command = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            & $candidate -c 'import sys; assert sys.version_info >= (3, 8)' 2>$null
            if ($LASTEXITCODE -eq 0) { return $candidate }
        }
    }
    throw 'Python 3.8+ was not found. Activate your Conda environment or add python.exe to PATH.'
}

[IO.Directory]::CreateDirectory((Split-Path -Parent $PidFile)) | Out-Null
if (Test-Path -LiteralPath $PidFile) {
    $existingPid = [int]([IO.File]::ReadAllText($PidFile).Trim())
    $existing = Get-Process -Id $existingPid -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "UI already running: http://127.0.0.1:$Port"
        return
    }
    Remove-Item -LiteralPath $PidFile -Force
}

$python = Find-Python
$oldPort = $env:ANY_SCIENCE_UI_PORT
$env:ANY_SCIENCE_UI_PORT = [string]$Port
try {
    $process = Start-Process -FilePath $python -ArgumentList 'ui/server.py' -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput $LogFile -RedirectStandardError $ErrorLog -PassThru
}
finally {
    if ($null -eq $oldPort) { Remove-Item Env:ANY_SCIENCE_UI_PORT -ErrorAction SilentlyContinue } else { $env:ANY_SCIENCE_UI_PORT = $oldPort }
}
[IO.File]::WriteAllText($PidFile, [string]$process.Id, $Utf8NoBom)

$url = "http://127.0.0.1:$Port/api/overview"
for ($attempt = 0; $attempt -lt 50; $attempt++) {
    if ($process.HasExited) { break }
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 1 | Out-Null
        Write-Host "UI started: http://127.0.0.1:$Port"
        if (-not $NoBrowser) { Start-Process "http://127.0.0.1:$Port" }
        return
    }
    catch {
        Start-Sleep -Milliseconds 100
    }
}

if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue
$details = if (Test-Path -LiteralPath $ErrorLog) { (Get-Content -Tail 20 -LiteralPath $ErrorLog) -join "`n" } else { 'no error log' }
throw "UI failed to start. $details"
