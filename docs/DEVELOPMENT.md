# Development guide

Full setup, day-to-day commands, and how to extend the app. For a quick
overview and the widget list, see the [README](../README.md); for what's
implemented in detail, see [FEATURES.md](FEATURES.md).

## Prerequisites

- Windows 10/11
- .NET 8 SDK — `winget install Microsoft.DotNet.SDK.8`
- iRacing (optional — everything below works against `--demo` without it)

**This machine's SDK is installed per-user**, not via winget, so it isn't on the
system PATH. Every command below assumes either:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
```

...has been run in the shell, or that `dotnet` is already resolvable (e.g. after
running the winget command above on a fresh machine). Check with `dotnet --version`
before assuming it's missing.

No iRacing SDK install is needed — telemetry comes from the
[IRSDKSharper](https://github.com/mherbold/IRSDKSharper) NuGet package, restored
automatically on build.

## Repo layout

```
IRacingOverlay.sln
Directory.Build.props          # shared MSBuild settings (see below)
src/
  IRacingOverlay.Core/          # domain logic - pure, tested, no UI/SDK deps
  IRacingOverlay.Infrastructure/# ITelemetrySource adapters (live + demo)
  IRacingOverlay.App/           # WPF windows, view models, composition root
tests/
  IRacingOverlay.Core.Tests/    # xUnit tests for Core
```

`Directory.Build.props` applies `Nullable`, `ImplicitUsings`, and
`TreatWarningsAsErrors` to every project — a compiler warning fails the build
solution-wide, by design.

| Project | TFM | Depends on |
|---|---|---|
| `IRacingOverlay.Core` | `net8.0` | — |
| `IRacingOverlay.Infrastructure` | `net8.0-windows` | `Core`, IRSDKSharper |
| `IRacingOverlay.App` | `net8.0-windows` (WPF) | `Core`, `Infrastructure`, CommunityToolkit.Mvvm |
| `IRacingOverlay.Core.Tests` | `net8.0` | `Core`, xUnit |

Dependencies point inward only: `App → Infrastructure → Core`. `Core` never
references WPF or the SDK, which is what makes it unit-testable without a
simulator or a UI thread.

## Day-to-day commands

```powershell
dotnet restore                 # first time / after pulling
dotnet build                   # whole solution; warnings are errors
dotnet test                    # Core.Tests only project with tests

# Run the app
dotnet run --project src/IRacingOverlay.App -- --demo   # simulated field, no iRacing
dotnet run --project src/IRacingOverlay.App              # live, waits for iRacing

# Or run the built exe directly (useful for scripted smoke tests)
src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.exe --demo
```

There's no watch/hot-reload script set up — `dotnet build` after each change is
fast enough (a couple of seconds) that it hasn't been worth adding.

## Debugging

- **Logic bugs (fuel maths, relative deltas, formatting):** write a failing xUnit
  test in `Core.Tests` first, then fix `Core`. This is almost always faster than
  attaching a debugger to the WPF app, and the test stays as a regression check.
- **Visual/layout issues:** open the `.sln` in Visual Studio or Rider and run the
  `IRacingOverlay.App` project with `--demo` as the launch argument; both support
  live XAML edits while running.
- **From a headless/CLI session (no IDE):** there's no way to *see* the rendered
  window, but you can still verify it renders and doesn't crash by launching the
  exe and checking the process stays alive:

  ```powershell
  $exe = "src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.exe"
  $p = Start-Process -FilePath $exe -ArgumentList "--demo" -PassThru
  Start-Sleep -Seconds 5
  -not $p.HasExited   # True = still running
  Stop-Process -Id $p.Id -Force
  ```

  To actually inspect the rendered UI without a human at the keyboard, take a
  full-screen capture with .NET's `System.Drawing`/`System.Windows.Forms` while
  the demo process is running, then crop to the widget region. This is how the
  navy-palette and layout redesigns in this repo were verified — see the git log
  around those commits for the exact script if you need it again.
- **Live iRacing not connecting:** confirm `irsdkEnableMem=1` in iRacing's
  `app.ini` (on by default) and that the sim is running in windowed or
  borderless mode — overlays don't draw over exclusive fullscreen.

## Adding a new widget

The pattern every widget so far follows (relative, fuel):

1. **Core calculator.** Pure class/static class in `src/IRacingOverlay.Core/<Area>/`,
   taking primitive inputs (or a `TelemetrySnapshot`) and returning a small
   record/struct result. No WPF, no SDK types.
2. **Tests.** `tests/IRacingOverlay.Core.Tests/<Area>/`, one test class per
   calculator, covering the normal case plus the edge cases that matter for sim
   telemetry: lap-counter jumps/resets, missing data, boundary values (0,
   negative, "unlimited" sentinels).
3. **Telemetry fields, if new ones are needed.** Add them to
   `TelemetrySnapshot` (`Core/Telemetry`), then populate both
   `IrsdkTelemetrySource` (real SDK var name — see IRSDKSharper's `GetFloat`/
   `GetInt`/etc., and use the `GetXOrDefault` helpers for vars that don't exist
   on all sim builds) **and** `SimulatedTelemetrySource` (a plausible constant
   or a simple formula) so demo mode stays in sync with live mode.
4. **View model.** `src/IRacingOverlay.App/ViewModels/`, inheriting
   `OverlayViewModelBase` for the shared connection-state handling. Keep it a
   thin translation from Core types to display strings/bools — no maths here.
5. **Window.** A borderless, transparent, topmost `Window` XAML file styled from
   the shared brushes/styles in `App.xaml` (see [FEATURES.md](FEATURES.md) for
   the current palette). Reuse the drag-to-move and right-click-to-exit pattern
   from `RelativeWindow.xaml.cs` / `FuelWindow.xaml.cs`.
6. **Wire it up** in `App.xaml.cs` (the composition root): construct the view
   model, subscribe it to the telemetry source's events (marshalled onto
   `Dispatcher`), construct the window with the view model as `DataContext`,
   call `.Show()`.
7. **Update the docs:** add the widget to [FEATURES.md](FEATURES.md) and, if the
   headline feature list changed, the [README](../README.md).

## Testing conventions

- Test names read as `Method_Scenario_ExpectedOutcome`.
- One assertion concept per test; use `[Theory]`/`[InlineData]` for formatting
  tables rather than one test per input.
- Every bug fix gets a regression test before or alongside the fix.
- UI code (`App` project) and the SDK adapter (`Infrastructure`) are not unit
  tested — that's deliberate, not a gap: they're thin glue over `Core`, which
  carries the coverage.

## Commits

Conventional-commit style, feature-sized: `feat(core): ...`, `fix(app): ...`,
`docs: ...`, `chore: ...`. Every commit should build clean and pass
`dotnet test` on its own — don't split a change across commits such that an
intermediate one is broken.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `dotnet : term not recognized` | SDK not on PATH — see Prerequisites above |
| Build fails on a warning | `TreatWarningsAsErrors` is on by design — fix the warning, don't suppress it |
| Demo window never appears | Check the process didn't exit immediately (`echo $?`/exit code) — a startup exception would show as a fast exit |
| Live mode stuck on "Waiting for iRacing" | `irsdkEnableMem=1` not set, sim not running, or sim is in exclusive fullscreen |
| A telemetry value is always 0/default in live mode | The SDK variable may not exist on your car/track/build — check it's read via `GetXOrDefault` in `IrsdkTelemetrySource`, not a bare `data.GetX(...)` which would throw |
