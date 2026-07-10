[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$WorkspacePath = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$WorkspacePath = [IO.Path]::GetFullPath($WorkspacePath)
$AssetRoot = Join-Path $PSScriptRoot 'assets\voice'
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

foreach ($relative in @('CLAUDE.md', 'PROTOCOL.md', '.claude\settings.json', 'scripts\validate.sh')) {
    if (-not (Test-Path -LiteralPath (Join-Path $WorkspacePath $relative) -PathType Leaf)) {
        throw "Not an Any Science workspace: missing $relative under $WorkspacePath"
    }
}
if (-not (Test-Path -LiteralPath $AssetRoot -PathType Container)) {
    throw "Voice assets are missing beside the installer: $AssetRoot"
}

Get-ChildItem -LiteralPath $AssetRoot -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($AssetRoot.Length).TrimStart('\', '/')
    $destination = Join-Path $WorkspacePath $relative
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $backup = "$destination.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss-fff')"
        Copy-Item -LiteralPath $destination -Destination $backup
        Write-Host "backup: $destination -> $backup"
    }
    [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
    $text = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($destination, $text, $Utf8NoBom)
}

Get-ChildItem -LiteralPath (Join-Path $WorkspacePath 'scripts\voice') -File -Filter '*.ps1' | ForEach-Object {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        throw "$($_.FullName) has PowerShell syntax errors: $($errors -join '; ')"
    }
}

Write-Host 'OK: Windows Voice extension installed. No packages or models were downloaded.'
Write-Host "Check local backends with: & '$WorkspacePath\scripts\voice\voice_status.ps1'"
