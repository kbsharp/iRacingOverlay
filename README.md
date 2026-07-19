# iRacing Overlay

A lightweight, always-on-top telemetry overlay for iRacing. Five widgets so far:

- **Standings**: the full, class-grouped field (top-left by default) — colour-tinted class
  banners with **Strength of Field**, then each car by class position with a
  **position-change arrow** (▲2 / ▼1 vs where it started the race), car number, a
  driver, colour-coded license and iRating badges, an optional (experimental) car
  manufacturer badge, interval to the car
  ahead, gap to the class leader, best lap (session-fastest in purple) and last-lap delta.
  Zebra rows, your own row highlighted, up to a full 40-car multiclass grid — all from the
  sim's real timing.
- **Relative** (the flagship glance): the three cars ahead of and behind you on track with
  live time deltas, race position and car number, kept **compact** (bottom by default) so
  it complements the standings rather than repeating it. Each row carries its class's
  actual colour from the sim; lapping cars are red, lapped cars blue, pitting cars flagged,
  your own row highlighted in amber. A session strip on top shows session type + time
  remaining, the lap counter (`L8/25`), a flag chip when one is flying, brake bias,
  track/air temps, a wetness badge, and your incidents against the session's limit
  (`2x/17x`, warming to amber then red as you approach it).
- **Fuel**: a strategy calculator — fuel level and laps in tank, average/last-lap burn,
  and the numbers you act on: fuel to finish, the margin you'll finish with (green spare
  / red short), fuel to add at the next stop, and a save-per-lap target. A strip along
  the bottom shows the **loaded setup file** and pulses the panel for the first minute of
  Qualifying or Race — a reminder to catch the classic "raced on the qualifying setup"
  mistake before it costs you a fuel-short race. Switch the strip off in Settings if you
  don't want it.
- **Radar**: an LMU-style top-down proximity radar — nearby cars drawn as class-coloured
  icons at their real positions relative to you, *angled to match the track* through
  corners. It hides itself when nobody's near and reappears the moment a car comes into
  range. iRacing exposes no position for other cars, so the radar learns the track's shape
  from your own driving over the first lap; until it's learned, it falls back to iRacing's
  coarse left/right spotter signal.

**Full docs:** [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for setup, commands, debugging,
and how to add a widget; [docs/FEATURES.md](docs/FEATURES.md) for exactly what's
implemented (every field, calculation, and known limitation);
[docs/STYLE.md](docs/STYLE.md) for the visual language and the active styling
refinement pass.

## Install (for the team)

No .NET, Visual Studio, or iRacing SDK needed — just Windows and iRacing. The
installer bundles everything else.

1. Open the [**Releases**](../../releases) page and download **`Setup.exe`** from the
   latest release.
2. Run it. Windows SmartScreen will flag an **"unknown publisher"** — the build
   isn't code-signed, which is normal for a small self-distributed app. Click
   **More info → Run anyway**.
3. It installs per-user to `%LocalAppData%\IRacingOverlay`, adds a Start-menu
   shortcut, and launches automatically.

From there see the usage notes below: control it from the **system tray icon**
(hidden behind the taskbar `^` overflow arrow the first time), run iRacing in
**windowed or borderless** mode, and drag each widget where you want it.

**Updates are automatic.** On launch the app quietly checks GitHub for a newer
release and downloads it in the background. When one is ready, the tray icon shows
a **"Restart to install update"** item (and a notification) — it never restarts
mid-session on its own, so you install it between races on your own schedule. You
can also trigger a check anytime from the tray's **Check for updates**.

*Publishing* a new version is a tagged push — see
[docs/DEVELOPMENT.md § Cutting a release](docs/DEVELOPMENT.md#cutting-a-release--step-by-step)
for the checklist.

## Prerequisites

- **To use it:** Windows 10/11 and iRacing — nothing else (see [Install](#install-for-the-team) above).
- **To build from source:** additionally the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — `winget install Microsoft.DotNet.SDK.8`.

There is **no separate iRacing SDK to install**: telemetry is read from the sim's
shared memory via the [IRSDKSharper](https://github.com/mherbold/IRSDKSharper)
NuGet package, which is restored automatically on first build.

## Build & run from source

For development. End users should use the [installer](#install-for-the-team)
instead. The fastest path — one command, and the app keeps running after the
terminal closes:

```powershell
.\scripts\run-demo.ps1   # simulated field, no iRacing needed
.\scripts\run-live.ps1   # waits for iRacing
```

To build and test:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

Or the plain dotnet commands (tied to the terminal that launched them). These
need `dotnet` on your PATH — the scripts above find a per-user SDK install too:

```powershell
dotnet run --project src/IRacingOverlay.App              # live
dotnet run --project src/IRacingOverlay.App -- --demo     # demo
```

Usage notes:

- **Control the app from the system tray icon**, not the terminal: **tick or untick
  each widget** to show or hide it (the choice is remembered), set the **UI scale**
  (100/125/150/175% — every widget together), open **Settings...**, open the dev control
  panel (demo mode), or Exit. New tray icons are hidden by Windows behind the
  **`^`** overflow arrow the first time — click it to find the icon, and drag it out onto
  the taskbar to pin it permanently.
- **Settings...** opens a normal window with everything that doesn't fit in a menu:
  per-widget scale and click-through, units (litres/gallons, °C/°F), the tuning
  numbers (fuel safety margin, setup flash duration, radar range, how many cars the
  relative and standings show), start-with-Windows, and a **Reset widget positions**
  button. There's no OK/Apply — changes apply as you make them.
- **Click-through** makes a widget ignore the mouse so clicks reach the sim. A
  click-through widget can't be dragged, so switch it back off in Settings if you
  need to move it.
- Closing a widget window (Alt+F4, etc.) just hides it — it's not gone, use the tray icon
  to bring it back. The tray's **Exit** (or a widget's right-click **Exit**) is what actually
  quits the app.
- **Demo mode** also opens a **dev control panel**: add/remove cars (up to a full 40-car
  grid), drain/add fuel, set fuel critical, cycle track wetness, add an incident, cycle the
  raised flag, toggle the
  player into the pits, cycle Practice/Qualify/Race (retriggers the setup-reminder flash),
  cycle the radar's spotter-fallback states — all live, no rebuild. See
  [docs/FEATURES.md](docs/FEATURES.md#dev-experience) for exact values.
- Drag each widget anywhere with the left mouse button. **Positions and the UI scale
  are remembered between runs** (saved to `%LocalAppData%\IRacingOverlay\settings.json`,
  or `%LocalAppData%\IRacingOverlay.Dev\` for a source build, so development can't
  disturb a racing layout);
  the first launch uses sensible defaults — standings top-left, relative bottom-left, the
  rest in a right column. A layout saved on a monitor you've since unplugged falls back to
  the default rather than opening off-screen.
- Panels are near-opaque with a top-lit material and soft 6px corners (RaceLab/LMU-style);
  the track shows only faintly through.
- iRacing must run in **windowed or borderless** mode — overlays are not visible over exclusive fullscreen.
- If no data appears while driving, check that `irsdkEnableMem=1` is set in iRacing's `app.ini` (it is by default).

## Architecture

Clean-architecture-lite; dependencies point inward (App → Infrastructure → Core):

| Project | Role |
|---|---|
| `src/IRacingOverlay.Core` | Domain: fuel/relative calculators, telemetry contracts, formatting. No UI or SDK dependencies. |
| `src/IRacingOverlay.Infrastructure` | `ITelemetrySource` adapters: IRSDKSharper (live) and a simulated source (demo). |
| `src/IRacingOverlay.App` | WPF presentation: overlay windows, view models, composition root, tray icon. |
| `tests/IRacingOverlay.Core.Tests` | xUnit tests for the domain layer. |

All widget maths live in `Core` and are unit-tested; the app is a thin shell over it.
See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the full architecture rundown,
the pattern for adding a new widget, and debugging notes; see
[docs/FEATURES.md](docs/FEATURES.md) for how each existing calculation actually works.

## Roadmap

The full roadmap lives in [docs/ROADMAP.md](docs/ROADMAP.md), built around
one test — *does it change a decision the driver makes in the car?* — with a
July 2026 market snapshot against RaceLab and iOverlay and the judgement
behind each call. The short version:

- **Core pass:** finish the widgets we have — a projected **Safety Rating**
  chip and **catch/defend pace forecasts** on the relative (neither
  competitor has either), pit status in the
  standings, a delta bar, and the carried-over polish list (radar density
  pass, manufacturer badge on the relative, drag-to-resize, a speed readout,
  configurable refresh rate, tray-icon pinning).
- **The strategy layer** (nobody has these): **pit-exit position
  projection** ("pit now and you rejoin P8, 1.8s behind #12"), a
  push-vs-save fuel tradeoff readout, and a multiclass **traffic
  meeting-point forecast** — race-engineer calls no current overlay makes.
- **Mid-term:** a **track map** built from the shape the radar already
  learns (no track database), a weather forecast strip, per-session-type
  settings profiles.
- **Parked or out, on purpose:** input trace and lap graphs wait until the
  core is strong; VR, gimmick data blocks (heart rate, G-force), column
  customization, streaming tools and team sharing are non-goals.

## Credits

Manufacturer badge marks come from [Simple Icons](https://simpleicons.org),
released under CC0. The logos themselves remain the respective manufacturers'
trademarks and are used here only to identify the car a driver is in.
