[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PidFile = Join-Path $Root '.claude\ui.pid'
if (-not (Test-Path -LiteralPath $PidFile -PathType Leaf)) {
    Write-Host 'UI is not running'
    return
}

$processId = [int]([IO.File]::ReadAllText($PidFile).Trim())
$process = Get-Process -Id $processId -ErrorAction SilentlyContinue
if (-not $process) {
    Remove-Item -LiteralPath $PidFile -Force
    Write-Host 'UI process already stopped'
    return
}

$cim = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
if (-not $cim -or $cim.CommandLine -notmatch 'ui[/\\]server\.py') {
    throw "PID $processId does not look like the Any Science UI server; refusing to stop it"
}

Stop-Process -Id $processId
Remove-Item -LiteralPath $PidFile -Force
Write-Host 'UI stopped'
