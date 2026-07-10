[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Text
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Text)) { return }
$Text = [regex]::Replace($Text, '\s+', ' ').Trim()
if ($Text.Length -gt 120) { $Text = $Text.Substring(0, 120) }

$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$log = Join-Path $Root 'workspace\voice\briefs.log'
[IO.Directory]::CreateDirectory((Split-Path -Parent $log)) | Out-Null
[IO.File]::AppendAllText($log, "$(Get-Date -Format s) $Text`n", [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Speech
$speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $speaker.Speak($Text)
}
finally {
    $speaker.Dispose()
}
