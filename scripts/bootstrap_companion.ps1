[CmdletBinding()]
param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

function Get-DotNet8Sdk {
    $candidates = @()
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }

    $userDotNet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if ((Test-Path -LiteralPath $userDotNet) -and $userDotNet -notin $candidates) {
        $candidates += $userDotNet
    }

    foreach ($candidate in $candidates) {
        $sdks = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -ne 0) {
            continue
        }

        $version = $sdks |
            ForEach-Object { if ($_ -match '^8\.0\.\d+') { $Matches[0] } } |
            Select-Object -Last 1
        if ($version) {
            return [pscustomobject]@{
                Version = $version
                Path = $candidate
            }
        }
    }

    return $null
}

$sdk = Get-DotNet8Sdk
if ($sdk) {
    Write-Host "AnyVoice Companion SDK ready: $($sdk.Version)"
    Write-Host "dotnet: $($sdk.Path)"
    exit 0
}

$installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$installScriptUri = 'https://dot.net/v1/dotnet-install.ps1'

if (-not $Install) {
    Write-Host 'AnyVoice Companion requires the .NET 8 SDK.'
    Write-Host 'Run this script again with -Install to use the official Microsoft non-admin installer.'
    Write-Host "Install directory: $installDir"
    exit 2
}

$temporaryScript = Join-Path ([IO.Path]::GetTempPath()) ("dotnet-install-{0}.ps1" -f [guid]::NewGuid().ToString('N'))
try {
    Write-Host "Downloading the official Microsoft installer from $installScriptUri"
    Invoke-WebRequest -UseBasicParsing -Uri $installScriptUri -OutFile $temporaryScript

    Write-Host "Installing .NET 8 SDK for the current user in $installDir"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $temporaryScript -Channel '8.0' -InstallDir $installDir -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-install.ps1 failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -LiteralPath $temporaryScript -Force -ErrorAction SilentlyContinue
}

$sdk = Get-DotNet8Sdk
if (-not $sdk) {
    throw 'The installation completed, but dotnet does not report an installed .NET 8 SDK.'
}

Write-Host "AnyVoice Companion SDK ready: $($sdk.Version)"
Write-Host "dotnet: $($sdk.Path)"
