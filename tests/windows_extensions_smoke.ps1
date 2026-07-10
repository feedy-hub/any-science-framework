[CmdletBinding()]
param(
    [switch]$UIOnly,
    [switch]$VoiceOnly
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
}

function Assert-PowerShellFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-True (Test-Path -LiteralPath $Path -PathType Leaf) "missing $Path"
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$errors
    )
    Assert-True ($errors.Count -eq 0) "$Path has parse errors: $($errors -join '; ')"

    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($Path))
    Assert-True (-not $text.Contains("`r")) "$Path contains CRLF or CR line endings"
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $parent = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    [IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

function New-WorkspaceFixture {
    $project = Join-Path ([IO.Path]::GetTempPath()) ('anyscience-windows-smoke-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($project) | Out-Null
    Write-Utf8NoBom (Join-Path $project 'CLAUDE.md') "# fixture`n"
    Write-Utf8NoBom (Join-Path $project 'PROTOCOL.md') "# fixture`n"
    Write-Utf8NoBom (Join-Path $project '.claude\settings.json') "{}`n"
    Write-Utf8NoBom (Join-Path $project 'scripts\validate.sh') "#!/bin/bash`nexit 0`n"
    Write-Utf8NoBom (Join-Path $project 'workspace\ideas\IDEA-001.md') "# Native Windows fixture`n- status: IDEA`n`nREVIEW: PENDING`n"
    Write-Utf8NoBom (Join-Path $project 'workspace\knowledge\insights.md') "# Insights`n"
    Write-Utf8NoBom (Join-Path $project 'workspace\knowledge\graveyard.md') "# Graveyard`n"
    return $project
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Get-HttpFailureStatus {
    param([Parameter(Mandatory = $true)][scriptblock]$Request)

    try {
        & $Request | Out-Null
        return 0
    }
    catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

function Test-WindowsUi {
    param([Parameter(Mandatory = $true)][string]$Installer)

    $project = New-WorkspaceFixture
    $started = $false
    try {
        & $Installer -WorkspacePath $project

        @(
            'ui\server.py',
            'ui\static\index.html',
            'scripts\ui_start.ps1',
            'scripts\ui_stop.ps1'
        ) | ForEach-Object {
            Assert-True (Test-Path -LiteralPath (Join-Path $project $_) -PathType Leaf) "UI installer did not create $_"
        }
        Assert-PowerShellFile (Join-Path $project 'scripts\ui_start.ps1')
        Assert-PowerShellFile (Join-Path $project 'scripts\ui_stop.ps1')

        $port = Get-FreeTcpPort
        & (Join-Path $project 'scripts\ui_start.ps1') -Port $port -NoBrowser
        $started = $true

        $overview = Invoke-RestMethod -Uri "http://127.0.0.1:$port/api/overview" -TimeoutSec 3
        Assert-True ($overview.cards.Count -eq 1) 'UI overview did not return the fixture card'
        Assert-True ($overview.cards[0].title -eq 'Native Windows fixture') 'UI card title parsing failed'

        $pathStatus = Get-HttpFailureStatus {
            Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$port/api/detail?path=../CLAUDE.md" -TimeoutSec 3
        }
        Assert-True ($pathStatus -eq 403) "path traversal returned HTTP $pathStatus instead of 403"

        $payloadStatus = Get-HttpFailureStatus {
            Invoke-WebRequest -UseBasicParsing -Method Post -Uri "http://127.0.0.1:$port/api/inbox" -ContentType 'application/json' -Body '[]' -TimeoutSec 3
        }
        Assert-True ($payloadStatus -eq 400) "array JSON returned HTTP $payloadStatus instead of 400"

        $response = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$port/api/inbox" -ContentType 'application/json' -Body '{"message":"native request"}' -TimeoutSec 3
        Assert-True ([bool]$response.ok) 'valid inbox request was rejected'
        Assert-True (Test-Path -LiteralPath (Join-Path $project ($response.file -replace '/', '\'))) 'inbox response referenced a missing file'
    }
    finally {
        if ($started -and (Test-Path -LiteralPath (Join-Path $project 'scripts\ui_stop.ps1'))) {
            & (Join-Path $project 'scripts\ui_stop.ps1')
        }
        if (Test-Path -LiteralPath $project) {
            Remove-Item -LiteralPath $project -Recurse -Force
        }
    }
}

$BuildScript = Join-Path $Root 'scripts\build.ps1'
Assert-True (Test-Path -LiteralPath $BuildScript -PathType Leaf) 'scripts/build.ps1 is required'
& $BuildScript

$UiInstaller = Join-Path $Root 'dist\extensions\setup_ui.ps1'
$VoiceInstaller = Join-Path $Root 'dist\extensions\setup_voice.ps1'

if (-not $VoiceOnly) {
    Assert-PowerShellFile $UiInstaller
    Test-WindowsUi -Installer $UiInstaller
}
if (-not $UIOnly) {
    Assert-PowerShellFile $VoiceInstaller
}

Write-Host 'OK: Windows extension smoke tests passed'
