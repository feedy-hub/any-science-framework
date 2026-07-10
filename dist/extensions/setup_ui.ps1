[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$WorkspacePath = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$WorkspacePath = [IO.Path]::GetFullPath($WorkspacePath)
$AssetRoot = Join-Path $PSScriptRoot 'assets\ui'
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

function Assert-AnyScienceWorkspace {
    foreach ($relative in @('CLAUDE.md', 'PROTOCOL.md', '.claude\settings.json', 'scripts\validate.sh')) {
        if (-not (Test-Path -LiteralPath (Join-Path $WorkspacePath $relative) -PathType Leaf)) {
            throw "Not an Any Science workspace: missing $relative under $WorkspacePath"
        }
    }
}

function Backup-File {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $backup = "$Path.bak.$stamp"
        Copy-Item -LiteralPath $Path -Destination $backup
        Write-Host "backup: $Path -> $backup"
    }
}

function Install-TextAsset {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Backup-File $Destination
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Destination)) | Out-Null
    $text = [IO.File]::ReadAllText($Source, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($Destination, $text, $Utf8NoBom)
}

function Find-Python {
    $candidates = @()
    if ($env:CONDA_PREFIX) {
        $candidates += (Join-Path $env:CONDA_PREFIX 'python.exe')
    }
    $command = Get-Command python.exe -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            continue
        }
        & $candidate -c 'import sys; assert sys.version_info >= (3, 8)' 2>$null
        if ($LASTEXITCODE -eq 0) {
            return $candidate
        }
    }
    throw 'Python 3.8+ was not found. Activate your Conda environment or add python.exe to PATH.'
}

Assert-AnyScienceWorkspace
if (-not (Test-Path -LiteralPath $AssetRoot -PathType Container)) {
    throw "UI assets are missing beside the installer: $AssetRoot"
}

Get-ChildItem -LiteralPath $AssetRoot -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($AssetRoot.Length).TrimStart('\', '/')
    Install-TextAsset -Source $_.FullName -Destination (Join-Path $WorkspacePath $relative)
}

$python = Find-Python
& $python -m py_compile (Join-Path $WorkspacePath 'ui\server.py')
if ($LASTEXITCODE -ne 0) {
    throw 'ui/server.py failed Python syntax validation'
}

Write-Host 'OK: Windows UI extension installed.'
Write-Host "Start it with: & '$WorkspacePath\scripts\ui_start.ps1'"
