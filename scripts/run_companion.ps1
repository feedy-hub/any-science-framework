[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

. (Join-Path $PSScriptRoot 'resolve_companion_dotnet.ps1')
$dotnet = Resolve-CompanionDotNet
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$desktopProject = Join-Path $repositoryRoot 'companion\src\AnyVoice.Desktop\AnyVoice.Desktop.csproj'

Push-Location $repositoryRoot
try {
    & $dotnet run --project $desktopProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "AnyVoice Companion exited with code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
