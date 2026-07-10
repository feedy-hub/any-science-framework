[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

. (Join-Path $PSScriptRoot 'resolve_companion_dotnet.ps1')
$dotnet = Resolve-CompanionDotNet
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'companion\AnyVoiceCompanion.sln'
$tests = Join-Path $repositoryRoot 'companion\tests\AnyVoice.Tests\AnyVoice.Tests.csproj'

$parseErrors = @()
Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*companion*.ps1' -File | ForEach-Object {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile(
        $_.FullName,
        [ref]$tokens,
        [ref]$errors)
    $parseErrors += $errors
}

if ($parseErrors.Count -gt 0) {
    $parseErrors | ForEach-Object { Write-Error $_.Message }
    throw 'Companion PowerShell syntax validation failed.'
}

Push-Location $repositoryRoot
try {
    & $dotnet restore $solution
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    & $dotnet run --project $tests --configuration Release --no-restore -- all
    if ($LASTEXITCODE -ne 0) {
        throw "AnyVoice tests failed with exit code $LASTEXITCODE"
    }

    & $dotnet build $solution --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "AnyVoice Release build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
