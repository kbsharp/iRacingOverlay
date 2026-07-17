# iRacing Overlay

A lightweight, always-on-top telemetry overlay for iRacing. Five widgets so far:

- **Standings**: the full, class-grouped field (top-left by default) — colour-tinted class
  banners with **Strength of Field**, then each car by class position with car number,
  driver, colour-coded license and iRating badges, interval to the car ahead, gap to the
  class leader, best lap (session-fastest in purple) and last-lap delta. Zebra rows, your
  own row highlighted, up to a full 40-car multiclass grid — all from the sim's real timing.
- **Relative** (the flagship glance): the three cars ahead of and behind you on track with
  live time deltas, race position and car number, kept **compact** (bottom by default) so
  it complements the standings rather than repeating it. Each row carries its class's
  actual colour from the sim; lapping cars are red, lapped cars blue, pitting cars flagged,
  your own row highlighted in amber. A session strip on top shows session type + time
  remaining, brake bias, track/air temps, a wetness badge, and your incident count.
- **Fuel**: a strategy calculator — fuel level and laps in tank, average/last-lap burn,
  and the numbers you act on: fuel to finish, the margin you'll finish with (green spare
  / red short), fuel to add at the next stop, and a save-per-lap target.
- **Setup**: shows the currently loaded setup file and flashes for the first minute of
  Qualifying or Race — a reminder to catch the classic "raced on the qualifying setup"
  mistake before it costs you a fuel-short race.
- **Radar**: a RaceLab/LMU-style blind-spot indicator — the left/right zones around a
  car icon light up and pulse when iRacing's own spotter signal detects a car alongside,
  with a "2" badge when there are two.

**Full docs:** [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for setup, commands, debugging,
and how to add a widget; [docs/FEATURES.md](docs/FEATURES.md) for exactly what's
implemented (every field, calculation, and known limitation).

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

Or the plain dotnet commands (tied to the terminal that launched them):

```powershell
dotnet run --project src/IRacingOverlay.App              # live
dotnet run --project src/IRacingOverlay.App -- --demo     # demo
dotnet test
```

Usage notes:

- **Control the app from the system tray icon**, not the terminal: show/hide any
  widget, set the **UI scale** (100/125/150/175% — scales every widget together), open the
  dev control panel (demo mode), or Exit. New tray icons are hidden by Windows behind the
  **`^`** overflow arrow the first time — click it to find the icon, and drag it out onto
  the taskbar to pin it permanently.
- Closing a widget window (Alt+F4, etc.) just hides it — it's not gone, use the tray icon
  to bring it back. The tray's **Exit** (or a widget's right-click **Exit**) is what actually
  quits the app.
- **Demo mode** also opens a **dev control panel**: add/remove cars (up to a full 40-car
  grid), drain/add fuel, set fuel critical, cycle track wetness, add an incident, toggle the
  player into the pits, cycle Practice/Qualify/Race (retriggers the setup-reminder flash),
  cycle the radar through all six proximity states — all live, no rebuild. See
  [docs/FEATURES.md](docs/FEATURES.md#dev-experience) for exact values.
- Drag each widget anywhere with the left mouse button. Default positions (standings
  top-left, relative bottom-left, the rest in a right column) aren't remembered between
  runs yet — that's on the roadmap.
- Panels are near-opaque and flat/sharp-cornered (RaceLab/LMU-style); the track shows only
  faintly through.
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

- Delta bar (lap delta to session/all-time best)
- Car manufacturer badges on the relative (needs custom art assets)
- Drag-to-resize widgets and remembered window positions/scale
- Click-through mode
- Pin the tray icon and/or run at Windows startup
- Settings: units (L/gal, km/h / mph), refresh rate, widget scale
