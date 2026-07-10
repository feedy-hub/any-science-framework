function Resolve-CompanionDotNet {
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
        if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^8\.0\.\d+' })) {
            return $candidate
        }
    }

    throw 'The .NET 8 SDK is missing. Run scripts\bootstrap_companion.ps1 -Install first.'
}
