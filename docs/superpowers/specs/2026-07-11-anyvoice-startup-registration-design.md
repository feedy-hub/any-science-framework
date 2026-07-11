# AnyVoice Windows Startup Registration Design

## Goal

Add an optional "Start with Windows" setting to AnyVoice Companion. It is disabled by default, applies only to the current Windows user, requires no administrator privileges, and can be turned off from the same settings page.

## Selected Approach

Use the `AnyVoiceCompanion` value under:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

The value launches the current Companion entry assembly through the .NET 8 host that is already running the application. This avoids the system-level .NET 7 resolution problem that occurs when the framework-dependent executable is launched directly.

The application only owns this one registry value. It must not enumerate, edit, or remove other startup entries.

## Settings And UI

- Add `StartWithWindows` to `CompanionSettings`.
- The default is `false`.
- Add one checkbox to the General settings tab.
- Saving settings reconciles the registry value before persisting the new JSON configuration.
- If registry registration fails, keep the previous setting and show a non-fatal warning.
- Enabling the option does not launch another Companion instance immediately.

## Default Language

Simplified Chinese is the default desktop language. Existing static user-visible text in the character menu, tray menu, settings window, fallback status messages, warnings, and dictation summaries is changed to Chinese in this implementation.

Protocol names, JSON property names, command-line interfaces, registry value names, source identifiers, and code APIs remain in English for compatibility. A runtime language selector and a general localization framework are outside this change.

## Components

`StartupLaunchCommand` builds a quoted command from a verified `dotnet.exe` path and the current entry assembly path. Both paths must be absolute existing files.

`StartupRegistrationService` owns enable, disable, and current-state checks against a small value-store interface. Tests use an in-memory store.

`WindowsStartupValueStore` is the production adapter backed by `Microsoft.Win32.Registry.CurrentUser`. It reads, writes, or deletes only the `AnyVoiceCompanion` value.

At application startup, the saved setting is reconciled with the registry so an updated installation path repairs a stale command. Reconciliation failure reports an error but does not prevent AnyVoice from starting.

## Error And Security Boundaries

- Never write to `HKLM`, the Task Scheduler, services, or the Startup folder.
- Never request elevation.
- Quote both executable and assembly paths; do not invoke a shell.
- Do not place model, microphone, transcript, or secret data in the startup command.
- Disable removes only the exact AnyVoice value and treats an absent value as success.
- Registry and path failures are converted to a stable startup-registration error without exposing sensitive paths in the UI.

## Verification

- Test the default and JSON round trip for `StartWithWindows`.
- Test command quoting and invalid-path rejection with temporary files.
- Test enable, disable, idempotence, stale-command replacement, and store failure using a fake value store.
- Build the WPF solution and run the full offline suite.
- Perform a current-user registry smoke test with backup and restoration of the exact AnyVoice value; do not modify any other startup entry.
