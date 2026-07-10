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
$ServerPath = (Resolve-Path -LiteralPath (Join-Path $Root 'ui\server.py')).Path
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
    $rawMetadata = [IO.File]::ReadAllText($PidFile).Trim()
    $metadata = $null
    try { $metadata = $rawMetadata | ConvertFrom-Json } catch { }
    $metadataFields = if ($metadata) { @($metadata.PSObject.Properties.Name) } else { @() }
    $metadataValid = $metadataFields -contains 'pid' -and $metadataFields -contains 'port' -and $metadataFields -contains 'workspace' -and $metadataFields -contains 'server'
    $existingPid = if ($metadataValid) { [int]$metadata.pid } elseif ($rawMetadata -match '^\d+$') { [int]$rawMetadata } else { 0 }
    $existing = if ($existingPid) { Get-Process -Id $existingPid -ErrorAction SilentlyContinue } else { $null }
    if ($existing) {
        $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$existingPid" -ErrorAction SilentlyContinue
        $expectedServer = if ($metadataValid) { [string]$metadata.server } else { $ServerPath }
        $isUi = $cim -and $cim.CommandLine -match [regex]::Escape($expectedServer)
        $isThisWorkspace = $metadataValid -and ([IO.Path]::GetFullPath([string]$metadata.workspace) -eq $Root)
        if ($isUi -and $isThisWorkspace) {
            $runningPort = [int]$metadata.port
            if ($runningPort -ne $Port) {
                throw "UI is already running on port $runningPort, not requested port $Port"
            }
            Write-Host "UI already running: http://127.0.0.1:$runningPort"
            return
        }
        if ($isUi -and -not $metadataValid) {
            throw 'A legacy UI process is still running. Stop it before restarting with the updated launcher.'
        }
    }
    Remove-Item -LiteralPath $PidFile -Force
}

$python = Find-Python
$oldPort = $env:ANY_SCIENCE_UI_PORT
$env:ANY_SCIENCE_UI_PORT = [string]$Port
try {
    $quotedServerPath = '"' + $ServerPath + '"'
    $process = Start-Process -FilePath $python -ArgumentList $quotedServerPath -WorkingDirectory $Root -WindowStyle Hidden -RedirectStandardOutput $LogFile -RedirectStandardError $ErrorLog -PassThru
}
finally {
    if ($null -eq $oldPort) { Remove-Item Env:ANY_SCIENCE_UI_PORT -ErrorAction SilentlyContinue } else { $env:ANY_SCIENCE_UI_PORT = $oldPort }
}
$metadata = [ordered]@{
    pid = $process.Id
    port = $Port
    workspace = $Root
    server = $ServerPath
}
[IO.File]::WriteAllText($PidFile, ($metadata | ConvertTo-Json -Compress), $Utf8NoBom)

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
