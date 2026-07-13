# iRacing Overlay — working guide

Lightweight, always-on-top WPF telemetry overlay for iRacing. MVP scope: build
small, but leave clean seams to scale. Widgets so far: relative (flagship), fuel.

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
- **README.** Update it when run steps, widgets, or prerequisites change.
- **Commit** in feature-sized chunks, conventional-commit style
  (`feat(core):`, `fix(app):`, `docs:`), each commit building green.

## Architecture

Clean-architecture-lite; dependencies point inward, `App → Infrastructure → Core`.

- **`Core`** — pure domain: fuel/relative calculators, telemetry & session contracts,
  formatting. No UI, no SDK, no `-windows` TFM. This is the tested layer.
- **`Infrastructure`** — `ITelemetrySource` adapters: IRSDKSharper (live) and the
  simulated `--demo` source. Reuse buffers for the `CarIdx*` arrays; no per-frame allocs.
- **`App`** — WPF windows + view models + the composition root in `App.xaml.cs`
  (manual DI; swap in a container only when wiring outgrows it).

## Patterns to keep

- **New widget = Core calculator (pure, tested) + view model + window.** Keep the maths
  out of the view model; the app stays a thin shell over `Core`.
- Telemetry events arrive on **background threads**; marshal to the UI thread in the
  composition root before touching a view model.
- The relative updates **fixed row slots in place** rather than rebuilding the list, to
  stay allocation-free and keep the layout stable — follow that for other list widgets.
- SDK vars missing on older sim builds must **degrade gracefully** (see the
  `GetIntOrDefault`/`GetFloatOrDefault` helpers), never throw.
- Format numbers with `InvariantCulture`; shared display logic goes in `Core/Formatting`.

## Behaviour

- MVP scope: minimal but extensible. Proceed on reversible work without asking; stop for
  destructive or scope-changing decisions.
- I can't see the rendered WPF UI from a headless session — call out layout/styling that
  needs a human eye rather than claiming it looks right.
