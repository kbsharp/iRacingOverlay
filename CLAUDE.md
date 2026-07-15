# iRacing Overlay — working guide

Lightweight, always-on-top WPF telemetry overlay for iRacing. MVP scope: build
small, but leave clean seams to scale. Widgets so far: standings (full
class-grouped field), relative (compact glance widget), fuel, setup (flashes
for the first minute of Qualify/Race as a reminder), radar (blind-spot
proximity, built on iRacing's own CarLeftRight spotter signal). The theme is
flat, sharp-cornered and near-opaque (RaceLab/LMU-style); a tray-menu UI-scale
control sizes every widget together. A system tray icon controls the app; demo
mode also shows a dev control panel.

## Build & run

The .NET 8 SDK is installed **per-user** at `%LOCALAPPDATA%\Microsoft\dotnet` and is
**not on the system PATH**. Prefix commands:

```powershell
$env:PATH="$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
dotnet build
dotnet test
dotnet run --project src/IRacingOverlay.App -- --demo   # no iRacing needed
```

(Once `winget install Microsoft.DotNet.SDK.8` has been run, the prefix is unneeded.)

## Definition of done — every change

- **Build clean.** `TreatWarningsAsErrors` is on; a warning is a failure. Never leave
  the tree in a state that doesn't build or has failing tests.
- **Tests.** Any logic in `Core` gets xUnit coverage. Bug fixes get a regression test.
  UI/SDK glue is exempt — that's why the logic lives in `Core` (see below).
- **Docs.** README changes when run steps, widgets, or prerequisites change.
  [docs/FEATURES.md](docs/FEATURES.md) changes whenever a widget's fields,
  calculations, thresholds, or limitations change — it's the canonical "what does
  this app currently do" reference, so it must stay accurate, not aspirational.
  [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) changes when the dev workflow itself
  changes (new commands, new conventions, new gotchas).
- **Commit** in feature-sized chunks, conventional-commit style
  (`feat(core):`, `fix(app):`, `docs:`), each commit building green.

## Architecture

Clean-architecture-lite; dependencies point inward, `App → Infrastructure → Core`.

- **`Core`** — pure domain: fuel/relative calculators, telemetry & session contracts,
  formatting. No UI, no SDK, no `-windows` TFM. This is the tested layer.
- **`Infrastructure`** — `ITelemetrySource` adapters: IRSDKSharper (live) and the
  simulated `--demo` source. Reuse buffers for the `CarIdx*` arrays; no per-frame allocs.
- **`App`** — WPF windows + view models + the composition root in `App.xaml.cs`
  (manual DI; swap in a container only when wiring outgrows it). Also has
  `UseWindowsForms` on (for the tray icon) alongside `UseWPF` — any type name that
  exists in both `System.Windows.*` and `System.Drawing`/`System.Windows.Forms`
  (seen so far: `Application`, `Color`, `Brush`) is ambiguous under the two SDKs'
  global usings. Fully qualify it, or add an explicit `using X = System.Windows.
  Media.X;` alias at the top of the file if it's used more than once or twice.

## Patterns to keep

- **New widget = Core calculator (pure, tested) + view model + window.** Keep the maths
  out of the view model; the app stays a thin shell over `Core`.
- Telemetry events arrive on **background threads**; marshal to the UI thread in the
  composition root before touching a view model.
- List widgets update **row slots in place**, not by rebuilding the collection, so
  ordering swaps don't flicker. The relative uses fixed slots; the standings uses a flat
  `ObservableCollection` (headers interleaved with rows) that only changes length when the
  field size does. Follow one of these for any new list.
- Prefer **time-based** reasoning over lap-count differences for gaps/laps-down. A raw
  completed-lap difference flickers to "+1L" for a car only tenths behind whenever the
  leader crosses the line; the standings derives laps-down from the time gap versus the
  class leader's best lap instead. (Found via a scratchpad dump harness — when a
  demo/live value looks wrong, instrument it rather than reason about it.)
- SDK vars missing on older sim builds must **degrade gracefully** (see the
  `GetIntOrDefault`/`GetFloatOrDefault` helpers), never throw.
- Format numbers with `InvariantCulture`; shared display logic goes in `Core/Formatting`.
- **Flashing/pulsing UI** (e.g. the setup reminder): a `Storyboard` inside a `Style`'s
  `DataTrigger.EnterActions` can't use `Storyboard.TargetName` to reach a sibling-named
  brush - `Style` triggers only have access to the element the style is attached to. Give
  that element its own local (non-shared, non-`StaticResource`) brush and target it via a
  property path instead, e.g. `Storyboard.TargetProperty="(Border.Background).
  (SolidColorBrush.Color)"`.
- A widget window closing must **hide, not exit the app** — subscribe
  `Closing += HideInsteadOfClose` in `App.xaml.cs` and add the window to
  `TrayIconService` (see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)). Only
  `App.RequestExit()` should ever call `Shutdown()`.
- New demo-only controls go on `IDemoControls`, implemented by
  `SimulatedTelemetrySource` under its `_gate` lock (mutated from the UI thread,
  read from the background timer thread every tick). This file has no automated
  tests — a real `IndexOutOfRangeException` shipped here once. Stress-test any
  change with a throwaway console harness before trusting it; see
  [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md#dev-control-panel-demo-mode).
- **Prefer real sim data over invented colours/values when one exists.** The
  relative widget's class colouring uses iRacing's own `CarClassColor` rather
  than a made-up palette, and the license badge uses iRacing's actual
  license-class colours — both read as instantly familiar to anyone who's
  used the sim, which an invented scheme wouldn't. Check the SDK model
  (`IRacingSdkSessionInfo` via reflection, or the IRSDKSharper docs) before
  hardcoding a value that the sim might already provide.
- Keep colour **purposeful, not decorative**: each hue should mean one thing
  (class, license tier, iRating tier, lap status, "this is you"). Don't reach
  for the same accent colour for everything — that's what made the first pass
  at the relative widget feel flat ("too much blue") before this rework.

## Behaviour

- MVP scope: minimal but extensible. Proceed on reversible work without asking; stop for
  destructive or scope-changing decisions.
- I can't see the rendered WPF UI from a headless session — call out layout/styling that
  needs a human eye rather than claiming it looks right.
