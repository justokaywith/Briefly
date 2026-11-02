# Live Listen repair (unreleased)

Root cause: the active detector used only visible window titles and English UI Automation call controls. An older microphone detector existed but was not wired into this path. A minimized/browser call could therefore be missed. The toggle also had no durable preference.

This branch adds:

- Primary read-only Windows ConsentStore microphone-use evidence, matched to a live executable or package. Own capture, stopped records and rows without a live process are excluded. Known call titles enrich platform metadata; an unknown browser activity does not invent a platform or meeting title.
- UI Automation fallback for silent/muted supported calls. A positive mic signal works without a visible meeting tab; mic activity itself is only a possible meeting, not proof.
- Explicit Start / Ignore / Don't ask for this app. Closing defaults to Ignore. No capture begins merely because a mic is active. Ignore choices can be reset in Settings.
- Bounded atomic preference persistence for the toggle and ignored app identities. Detection runs only while enabled, signed in and the app is running (including minimized). OS startup and tray residency are not implemented in this increment.
- A Diagnostics view and bounded local event log with fixed event codes. Titles, transcripts, email, credentials, audio and executable paths are excluded from these event records. Local ignored-app preferences contain executable identities and should be treated as private device settings.

New modules: `src/ListenPreferences.cs` and `src/DesktopDiagnostics.cs`. Public build script includes them. Backend and authentication protocol are unchanged.

## Manual acceptance

1. Enable Live Listen and accept its explanation. Restart the app; verify the toggle stays on. No recording should begin.
2. Start an unmuted Teams/Zoom or browser call. After two stable polling observations, check the possible-meeting prompt. Do not assume every Windows version/browser exposes the same metadata.
3. Choose Ignore: no recording. Choose Don't ask: the same app should not prompt until Reset ignored apps is used. Browser ignore applies to that browser app, not one website.
4. Choose Start only with participants informed. Verify actual mic and system audio and the stopped/transcribed/synced outputs separately. Those real-device acceptance checks are still required.
5. Start manual recording with no supported meeting. It must not immediately show call-ended confirmation. A detection-owned recording requires sustained absence and confirmation; unknown detector evidence is not an ended call.
6. Open Settings > Diagnostics. Verify state/step codes; no transcript/title/token should appear in the diagnostic events. Disable Live Listen: background detection should stop.

Automated tests cover pure mic-signal parsing/exclusions, stable app identity, bounded per-app preferences, corrupt-file default opt-out, per-app debounce, safe prompt defaults and native layout. A read-only adapter probe does not prove live-call accuracy. Neither does a fictional UIA fixture prove real Teams compatibility.

Current release feed/installer remains unchanged. No universal detection, improved audio accuracy, device-switch recovery or macOS support is claimed by this branch. Capture segments, separate-track timestamped transcription, larger optional local models and subsequent notes/features remain required work.
