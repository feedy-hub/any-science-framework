[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PidFile = Join-Path $Root '.claude\ui.pid'
if (-not (Test-Path -LiteralPath $PidFile -PathType Leaf)) {
    Write-Host 'UI is not running'
    return
}

$rawMetadata = [IO.File]::ReadAllText($PidFile).Trim()
$metadata = $null
try { $metadata = $rawMetadata | ConvertFrom-Json } catch { }
$metadataFields = if ($metadata) { @($metadata.PSObject.Properties.Name) } else { @() }
$metadataValid = $metadataFields -contains 'pid' -and $metadataFields -contains 'port' -and $metadataFields -contains 'workspace' -and $metadataFields -contains 'server'
$processId = if ($metadataValid) { [int]$metadata.pid } elseif ($rawMetadata -match '^\d+$') { [int]$rawMetadata } else { 0 }
if (-not $processId) {
    throw 'UI PID metadata is invalid; refusing to stop an unknown process'
}
$process = Get-Process -Id $processId -ErrorAction SilentlyContinue
if (-not $process) {
    Remove-Item -LiteralPath $PidFile -Force
    Write-Host 'UI process already stopped'
    return
}

$cim = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
if (-not $metadataValid) {
    throw 'Legacy UI PID metadata cannot identify the workspace safely; stop that process manually'
}
$expectedWorkspace = [IO.Path]::GetFullPath([string]$metadata.workspace)
$expectedServer = [IO.Path]::GetFullPath([string]$metadata.server)
if ($expectedWorkspace -ne $Root -or -not $cim -or $cim.CommandLine -notmatch [regex]::Escape($expectedServer)) {
    throw "PID $processId does not look like the Any Science UI server; refusing to stop it"
}

Stop-Process -Id $processId
Remove-Item -LiteralPath $PidFile -Force
Write-Host 'UI stopped'
