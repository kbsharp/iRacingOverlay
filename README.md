# iRacing Overlay

A lightweight, always-on-top telemetry overlay for iRacing. Two widgets so far:

- **Relative** (the flagship): the three cars ahead of and behind you on track with
  live time deltas, race position, car number, license and iRating. Lapping cars are
  red, lapped cars blue, pitting cars flagged. A session strip on top shows session
  type + time remaining, brake bias, track/air temps, a wetness badge, and your
  incident count.
- **Fuel**: fuel level, average and last-lap burn, estimated laps remaining, plus
  live gear and speed.

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — `winget install Microsoft.DotNet.SDK.8`
- iRacing (only for live telemetry — demo mode runs without it)

There is **no separate iRacing SDK to install**: telemetry is read from the sim's
shared memory via the [IRSDKSharper](https://github.com/mherbold/IRSDKSharper)
NuGet package, which is restored automatically on first build.

## Run

```powershell
# Live - connects when the iRacing sim starts broadcasting
dotnet run --project src/IRacingOverlay.App

# Demo - simulated laps, no iRacing needed
dotnet run --project src/IRacingOverlay.App -- --demo

# Tests
dotnet test
```

Usage notes:

- Drag each widget anywhere with the left mouse button; right-click either and choose **Exit** to quit.
- iRacing must run in **windowed or borderless** mode — overlays are not visible over exclusive fullscreen.
- If no data appears while driving, check that `irsdkEnableMem=1` is set in iRacing's `app.ini` (it is by default).

## Architecture

Clean-architecture-lite; dependencies point inward (App → Infrastructure → Core):

| Project | Role |
|---|---|
| `src/IRacingOverlay.Core` | Domain: fuel calculator, telemetry contracts, formatting. No UI or SDK dependencies. |
| `src/IRacingOverlay.Infrastructure` | `ITelemetrySource` adapters: IRSDKSharper (live) and a simulated source (demo). |
| `src/IRacingOverlay.App` | WPF presentation: overlay window, view model, composition root. |
| `tests/IRacingOverlay.Core.Tests` | xUnit tests for the domain layer. |

Design notes:

- All widget maths live in `Core` and are unit-tested: the fuel calculator (refuels,
  tows, lap-counter jumps, rolling window) and the relative engine (`CarIdxEstTime`
  deltas with start/finish wrap correction, lapped/lapping classification, roster
  joins). The app is a thin shell.
- Telemetry events arrive on background threads at ~15 Hz (the sim's 60 Hz frames are
  throttled in the adapter to keep the footprint small) and are marshalled to the UI
  thread in `App.xaml.cs`.
- Wiring is a manual composition root in `App.xaml.cs`; swap in a DI container once
  there is more than one widget to compose.

## Roadmap

- Radar/spotter widget (proximity warning for cars alongside, RaceLab-style)
- Standings widget (full field, gaps, multiclass split)
- Fuel-to-finish using race laps remaining from session info
- Delta bar (lap delta to session/all-time best)
- Multiclass class colours on the relative
- Click-through mode and remembered window positions
- Settings: units (L/gal, km/h / mph), refresh rate, widget scale
