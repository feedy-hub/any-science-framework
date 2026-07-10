[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Text
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Text)) { return }
$Text = [regex]::Replace($Text, '\s+', ' ').Trim()
if ($Text.Length -gt 120) { $Text = $Text.Substring(0, 120) }

Add-Type -AssemblyName System.Speech
$speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $speaker.Speak($Text)
}
finally {
    $speaker.Dispose()
}
