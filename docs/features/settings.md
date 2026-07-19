# Settings & layout persistence

`SettingsWindow` / `SettingsViewModel` / `Core.Settings` / `Services.SettingsService`

## The settings window

The user-facing control surface for everything that doesn't fit in a tray menu.
Opened from the tray's **Settings...**; created lazily, since most sessions
never open it.

**It is deliberately a normal window** — standard chrome, a taskbar entry, and
no `AllowsTransparency`. Every other window here is a borderless transparent
overlay because it sits over the sim, but that also costs it ClearType (see
[shell](shell.md#typography)). The settings window is used alt-tabbed, in the
pits, so it gets proper subpixel text rendering and a title bar you can close.
Reusing the overlay panel material here would inherit the greyscale-AA problem
for no benefit.

**There is no OK/Apply.** Every control writes straight through to
`SettingsService`, which raises `Changed`, which makes `App.ApplySettings` push
the new state at every widget. The point is watching the overlay react while you
adjust it — a form you fill in and submit would hide exactly the feedback that
makes these numbers choosable.

| Section | Controls |
|---|---|
| **Widgets** | Per widget: on/off, a scale override (100/125/150/175%), and click-through. |
| **Units** | Fuel litres/gallons, temperature °C/°F, speed km/h / mph. |
| **Tuning** | Fuel safety margin (0–5 laps), setup flash (5–300 s), radar range (15–200 m), relative cars each side (1–8), standings cars per class (5–60). |
| **General** | Start with Windows; only show widgets while iRacing is running; **Reset widget positions**. |

- **Per-widget scale** overrides the shared tray scale for that widget only — a
  standings table and a radar rarely want the same size. Absent override =
  follow the shared scale.
- **Click-through** (`WindowInterop.SetClickThrough`, `WS_EX_TRANSPARENT`) makes
  a widget ignore the mouse so clicks reach the sim. It's **per widget, not
  global**, because a click-through widget can't be dragged — the settings
  window is the only way back, so making it all-or-nothing would risk stranding
  the whole layout.
- **Units convert at format time only** (`Core.Formatting.UnitFormat`).
  Telemetry is normalised to metric on the way in and every calculation stays
  metric, so the unit can be flipped mid-session without invalidating a rolling
  fuel average or a lap-time window. Fuel keeps 2dp and temperature whole
  degrees in both systems so a column doesn't change width when the unit
  changes. No widget renders a speed yet — that preference is here ahead of a
  readout that uses it.
- **Tuning** feeds the Core calculators, each of which already took the value as
  a parameter. `WidgetTuning`'s defaults are the literal constants those
  calculators used before, so an untouched settings file reproduces the previous
  behaviour exactly. `SetupReminderTracker.FlashDurationSeconds` is a settable
  property rather than a `const`; changing it mid-flash ends the flash on the
  next frame rather than finishing the old window.
- **Reset widget positions** forgets every saved position so each widget returns
  to its default corner next launch — the recovery path for a layout dragged
  somewhere unusable.
- **Start with Windows** writes a per-user `HKCU\...\Run` entry
  (`StartupService`). No elevation needed, and it matches where Velopack
  installs the app. The registered path is `Environment.ProcessPath` — under
  Velopack the stub launcher above `current\`, so the entry survives
  auto-updates. It persists **the state the registry write actually achieved**,
  not the one requested: a locked-down machine can refuse the write, and the
  checkbox shouldn't then claim an autostart entry that doesn't exist. Startup
  re-asserts the entry if it's meant to be on, in case an update moved the
  executable.
- **Only show widgets while iRacing is running** (on by default,
  `OverlaySettings.HideWhenSimClosed`) keeps every widget hidden until telemetry
  connects, and hides them again when the sim closes. This is what makes *Start
  with Windows* usable: without it, switching autostart on leaves a set of
  always-on-top panels over the desktop for the rest of the day. The rule is one
  pure function — `Core.Settings.WidgetVisibility.ShouldShow(isEnabled,
  isSimConnected, hideWhenSimClosed)` — and it is **the single decision point**:
  startup, a settings change, and a connect/disconnect all route through
  `App.ShouldShow`, so there's no second path that can put a widget on screen.
  The tray icon stays visible throughout, so the app is never lost. Switch the
  option off to position widgets with iRacing shut; demo mode counts as
  connected, so `--demo` is unaffected.

**Controls are retemplated**, not stock. WPF's default CheckBox / RadioButton /
Slider / ComboBox / ScrollBar render in the system's light chrome, which against
this dark panel reads as a different application pasted into the window. The
`ControlTemplate`s live in `SettingsWindow.xaml`'s own resources rather than
`App.xaml` — the overlay widgets use none of these control types, so a set of
implicit styles in the shared dictionary would be a trap for whoever adds the
first one. Shared palette: `#262D38` fill, `#46525F` edge, `Accent` for the "on"
state.

### Known limitations

- Scale is a fixed set of four steps, not free resizing (drag-to-resize is a
  roadmap item).
- The window has no automated tests — it's WPF glue. The model underneath it
  (`OverlaySettings`, `WidgetTuning`, `UnitPreferences`, `UnitFormat`) is fully
  covered. Review it visually with `scripts\render.ps1 settings`.

## Layout persistence

The UI scale and every widget's window position are remembered between runs, so
the app comes back the way it was left instead of resetting to the default
corners.

- Saved to `%LocalAppData%\IRacingOverlay\` as `OverlaySettings` —
  **`settings.json` for the installed app, `settings.dev.json` for anything
  else** (`dotnet run`, a portable unzip, a build run straight out of `bin\`).
  `SettingsLocation.FileNameFor` picks between them from
  `UpdateManager.IsInstalled`. **Keep that split.** With one shared file a dev
  session loaded the layout arranged for real racing and wrote back wherever the
  dev windows landed; with both open, each debounce-saved the whole file and the
  last writer won.
- That path is in the Velopack install root, **above** the versioned `current\`
  folder, so it survives auto-updates and is removed on uninstall.
- Contents: the shared `Scale`, a `WindowPosition` per widget, the per-widget
  enabled/scale/click-through maps, `Units`, `Tuning`, `RunAtStartup` and
  `HideWhenSimClosed`.
- Every per-widget map is keyed by `WidgetIds` and is **sparse**: an absent key
  means the default, so adding a widget can't leave it switched off for existing
  users and a fresh install writes almost nothing.
- **`WidgetIds` values equal the window type names** the original layout code
  used, kept verbatim so existing files still restore. Changing one silently
  resets every user's saved position for that widget.
- Positions are captured on each window's `LocationChanged` and **debounced**
  (750 ms) so a drag doesn't hammer the disk; a final flush runs on exit and
  before an update-restart. Scale is saved when picked from the tray.
- On launch, `App.RestorePosition` reapplies each saved position **only if it's
  still on a connected display** (`LayoutGuard.IsOnScreen` against the virtual
  desktop), so a layout saved on a since-unplugged monitor falls back to the
  default rather than opening off-screen. The saved scale is applied via
  `App.SetScale` and reflected as a tick in the tray's UI Scale submenu.
- The pure model, serializer (forgiving of a missing/corrupt file), scale
  sanitizing and on-screen check live in `Core.Settings` and are unit-tested;
  the file I/O and WPF wiring are untested glue.

**Anything else worth remembering across runs belongs in `OverlaySettings`, not
a new file.** A new field must be *additive* — an existing `settings.json`
predates it, so absent must mean "previous behaviour".
