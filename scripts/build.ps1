[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

function Copy-ReleaseText {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $text = [IO.File]::ReadAllText($Source, [Text.Encoding]::UTF8)
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $parent = Split-Path -Parent $Destination
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    [IO.File]::WriteAllText($Destination, $text, $Utf8NoBom)
}

function Assert-PowerShellSyntax {
    param([Parameter(Mandatory = $true)][string]$Path)

    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$errors
    )
    if ($errors.Count -gt 0) {
        throw "$Path has PowerShell syntax errors: $($errors -join '; ')"
    }
}

$Dist = Join-Path $Root 'dist'
$DistExtensions = Join-Path $Dist 'extensions'
[IO.Directory]::CreateDirectory($DistExtensions) | Out-Null

$sourceSetupFiles = Get-ChildItem -LiteralPath (Join-Path $Root 'src') -File -Filter '*.ps1'
foreach ($file in $sourceSetupFiles) {
    $destination = Join-Path $Dist $file.Name
    Copy-ReleaseText -Source $file.FullName -Destination $destination
    Assert-PowerShellSyntax $destination
}

$sourceExtensionFiles = Get-ChildItem -LiteralPath (Join-Path $Root 'src\extensions') -File -Filter '*.ps1'
foreach ($file in $sourceExtensionFiles) {
    $destination = Join-Path $DistExtensions $file.Name
    Copy-ReleaseText -Source $file.FullName -Destination $destination
    Assert-PowerShellSyntax $destination
}

$assetRoot = Join-Path $Root 'src\extensions\assets'
if (Test-Path -LiteralPath $assetRoot -PathType Container) {
    Get-ChildItem -LiteralPath $assetRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($assetRoot.Length).TrimStart('\', '/')
        Copy-ReleaseText -Source $_.FullName -Destination (Join-Path $DistExtensions "assets\$relative")
    }
}

Write-Host 'OK: Windows release scripts built in dist/'
