# iRacing Overlay

A lightweight, always-on-top telemetry overlay for iRacing. Two widgets so far:

- **Relative** (the flagship): the three cars ahead of and behind you on track with
  live time deltas, race position, car number, license and iRating. Lapping cars are
  red, lapped cars blue, pitting cars flagged. A session strip on top shows session
  type + time remaining, brake bias, track/air temps, a wetness badge, and your
  incident count.
- **Fuel**: a strategy calculator — fuel level and laps in tank, average/last-lap burn,
  and the numbers you act on: fuel to finish, the margin you'll finish with (green spare
  / red short), fuel to add at the next stop, and a save-per-lap target.

**Full docs:** [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for setup, commands, debugging,
and how to add a widget; [docs/FEATURES.md](docs/FEATURES.md) for exactly what's
implemented (every field, calculation, and known limitation).

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — `winget install Microsoft.DotNet.SDK.8`
- iRacing (only for live telemetry — demo mode runs without it)

There is **no separate iRacing SDK to install**: telemetry is read from the sim's
shared memory via the [IRSDKSharper](https://github.com/mherbold/IRSDKSharper)
NuGet package, which is restored automatically on first build.

## Run

The fastest path — one command, and the app keeps running after the terminal closes:

```powershell
.\scripts\run-demo.ps1   # simulated field, no iRacing needed
.\scripts\run-live.ps1   # waits for iRacing
```

Or the plain dotnet commands (tied to the terminal that launched them):

```powershell
dotnet run --project src/IRacingOverlay.App              # live
dotnet run --project src/IRacingOverlay.App -- --demo     # demo
dotnet test
```

Usage notes:

- **Control the app from the system tray icon**, not the terminal: show/hide either
  widget, open the dev control panel (demo mode), or Exit. New tray icons are hidden by
  Windows behind the **`^`** overflow arrow the first time — click it to find the icon, and
  drag it out onto the taskbar to pin it permanently.
- Closing a widget window (Alt+F4, etc.) just hides it — it's not gone, use the tray icon
  to bring it back. The tray's **Exit** (or a widget's right-click **Exit**) is what actually
  quits the app.
- **Demo mode** also opens a **dev control panel**: add/remove cars (3-20), drain/add fuel,
  set fuel critical, cycle track wetness, add an incident, toggle the player into the pits —
  all live, no rebuild. See [docs/FEATURES.md](docs/FEATURES.md#dev-experience) for exact values.
- Drag each widget anywhere with the left mouse button.
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

- Radar/spotter widget (proximity warning for cars alongside, RaceLab-style)
- Standings widget (full field, gaps, multiclass split)
- Delta bar (lap delta to session/all-time best)
- Multiclass class colours on the relative
- Drag-to-resize widgets and remembered window positions/scale
- Click-through mode
- Pin the tray icon and/or run at Windows startup
- Settings: units (L/gal, km/h / mph), refresh rate, widget scale
