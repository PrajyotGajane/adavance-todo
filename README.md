# Desktop Companion (Phase 1 prototype)

Compact always-on-top task widget for Windows. C# / WPF / .NET 8, no third-party libraries. Data is hardcoded and in memory only.

## Build and run
Requires the .NET 8 SDK.

    dotnet run -c Release --project src/DesktopCompanion

## What is in Phase 1
- Frameless dark widget (drag by header, double-click header or menu to toggle compact/expanded)
- Today / Upcoming / Backlog / Later tabs, checkboxes with completed styling
- Global shortcuts (work from any app): Ctrl+Shift+N opens quick add, Ctrl+Alt+N toggles the scratchpad. Registered with `RegisterHotKey` (no polling); a warning appears if another app already owns a combo. Rebinding is not implemented yet.
- Quick add overlay (+ button, menu or Ctrl+Shift+N): Enter adds to Today, Esc closes
- Scratchpad (menu "Add note" or Ctrl+Alt+N): text is kept while hidden, Save/Ctrl+S adds to the in-memory Notes list
- Notes list window (menu "Notes")
- "Minimize to tray" / "Hide window" minimize to the taskbar for now (no tray yet)
- Settings / Appearance / Shortcuts are stubs

Not implemented: persistence, auto-save, configurable hotkeys, tray, drag-and-drop between tabs.

## Performance report (Release build, Windows)
Measured with `Process.PrivateMemorySize64` / `WorkingSet64` after the app settled (8 s):

| State | Private memory | Working set |
|---|---|---|
| Idle | ~44 MB | ~10-12 MB (trimmed) |
| After 15 quick-add cycles | ~52 MB | ~7-10 MB (trimmed) |

Without trimming, the working set was 92 MB idle and 105 MB after using overlays, so the app trims the working set when idle (`App.TrimMemory`) and creates overlay windows lazily. Idle CPU is ~0.

