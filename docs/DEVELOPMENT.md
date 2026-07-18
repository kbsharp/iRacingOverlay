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
  IRacingOverlay.App/           # WPF windows, view models, composition root, tray icon
tests/
  IRacingOverlay.Core.Tests/    # xUnit tests for Core
scripts/
  run-demo.ps1                  # build + launch, detached from the terminal (--demo)
  run-live.ps1                  # build + launch, detached from the terminal
```

`Directory.Build.props` applies `Nullable`, `ImplicitUsings`, and
`TreatWarningsAsErrors` to every project — a compiler warning fails the build
solution-wide, by design.

| Project | TFM | Depends on |
|---|---|---|
| `IRacingOverlay.Core` | `net8.0` | — |
| `IRacingOverlay.Infrastructure` | `net8.0-windows` | `Core`, IRSDKSharper |
| `IRacingOverlay.App` | `net8.0-windows` (WPF + `UseWindowsForms`) | `Core`, `Infrastructure`, CommunityToolkit.Mvvm |
| `IRacingOverlay.Core.Tests` | `net8.0` | `Core`, xUnit |

Dependencies point inward only: `App → Infrastructure → Core`. `Core` never
references WPF or the SDK, which is what makes it unit-testable without a
simulator or a UI thread.

## Day-to-day commands

```powershell
dotnet restore                 # first time / after pulling
dotnet build                   # whole solution; warnings are errors
dotnet test                    # Core.Tests only project with tests

# Run the app - detached from the terminal (see "Window lifecycle" below)
.\scripts\run-demo.ps1
.\scripts\run-live.ps1

# Or run it tied to the terminal, e.g. to see startup exceptions live
dotnet run --project src/IRacingOverlay.App -- --demo
dotnet run --project src/IRacingOverlay.App

# Or the built exe directly (useful for scripted smoke tests)
src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.exe --demo
```

There's no watch/hot-reload script set up — `dotnet build` after each change is
fast enough (a couple of seconds) that it hasn't been worth adding.

**Before rebuilding, make sure no instance of the app is still running** — `dotnet
build` will fail with `MSB3026: ... The file is locked by "IRacingOverlay (pid)"`
if one holds the output DLLs open. This is easy to hit now that the app survives
window-close (see below) and detached launches don't visibly tie up a terminal:

```powershell
Get-Process IRacingOverlay -ErrorAction SilentlyContinue | Stop-Process -Force
```

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

  To actually **see** the rendered UI without a human at the keyboard, render the
  window offscreen to a PNG with a throwaway harness rather than screen-scraping.
  Screen capture is unreliable here — the widgets are `ShowInTaskbar="False"` /
  `WindowStyle="None"`, so they have no taskbar entry, they can sit behind other
  windows, and screen-capture tooling generally can't resolve them by name.

  That harness lives in the repo as [`tools/RenderWidget`](../tools/RenderWidget):

  ```powershell
  dotnet run --project tools/RenderWidget                            # standings.png
  dotnet run --project tools/RenderWidget -- relative out/rel.png    # pick widget + path
  ```

  It's deliberately **not** in `IRacingOverlay.sln`, so `dotnet build` and CI
  don't carry it — run it explicitly by path. It news up the real `App` for its
  `App.xaml` resources, drives `SimulatedTelemetrySource` for one frame, feeds
  the real view model, and `RenderTargetBitmap`s the real window's `Content` at
  2× DPI (192) over an opaque backdrop. Adding another widget is one `case` in
  `BuildWindow`.

  `RenderTargetBitmap` uses greyscale antialiasing exactly as the live
  `AllowsTransparency` windows do, so text weight comes out faithful — which is
  the whole point when the thing under review *is* the text. This is how the
  typography pass was verified, and how the manufacturer badges were sized (the
  first pass had McLaren's very wide mark collapsing to an invisible hairline
  inside a square box — only visible by looking).

  Two traps if you ever rewrite it:
  - **Never pump the dispatcher** (no `Show()`, no `Dispatcher.Invoke`).
    Constructing `App` queues `App.OnStartup` on the dispatcher; pumping runs the
    real composition root, which builds `UpdateService` and dies with "No
    VelopackLocator has been set". Drive `Measure`/`Arrange`/`UpdateLayout`
    manually instead — `StaticResource` still resolves via
    `Application.Current.Resources`.
  - It needs the same `Color`/`Brush`/`Size` alias workaround as `App` (see
    CLAUDE.md), because it also sets `UseWindowsForms`.
  - **Radar caveat:** the radar auto-hides until its `TrackMap` is learned
    (~one lap) *and* a car is in range, so a two-second demo warmup renders
    nothing. Either drive the sim a full lap, or feed `RadarViewModel` synthetic
    snapshots directly — step the player's `LapDistPct`/`PlayerYawRad` around a
    lap to warm the map, then a final frame with cars nearby. Put the player on a
    curved part of the heading function to see the car angles. One thing a render
    *can't* settle: whether left/right matches iRacing's live `Yaw` sign — confirm
    against the sim that a car on your left shows on your left.
- **Live iRacing not connecting:** confirm `irsdkEnableMem=1` in iRacing's
  `app.ini` (on by default) and that the sim is running in windowed or
  borderless mode — overlays don't draw over exclusive fullscreen.

## Window lifecycle & tray icon

The app runs under `ShutdownMode="OnExplicitShutdown"` (`App.xaml`), not the WPF
default. This means:

- Closing a widget window (Alt+F4, or anything that would normally destroy it)
  is intercepted by `App.HideInsteadOfClose` and just **hides** it instead. The
  window object is never destroyed, so it can be shown again.
- The **only** path that actually ends the process is `App.RequestExit()`, which
  sets a flag and calls `Shutdown()`. It's wired to the tray icon's Exit item and
  every window's right-click **Exit** menu item — never call
  `System.Windows.Application.Current.Shutdown()` directly from a new window, or
  it'll bypass the flag and `HideInsteadOfClose` will (harmlessly, but
  confusingly) try to cancel a shutdown that's already in progress.
- `TrayIconService` (`src/IRacingOverlay.App/Services/`) owns the
  `System.Windows.Forms.NotifyIcon` and its context menu (Show Relative, Show
  Fuel, Dev Controls if present, Exit). This exists because the widget windows
  are borderless/topmost with no taskbar entry — they can get lost behind a
  fullscreen game with no way back except the tray.
- **Type ambiguity gotcha:** the `App` project has both `UseWPF` and
  `UseWindowsForms` on, which both contribute a global `using` for a type named
  `Application` (`System.Windows.Application` vs `System.Windows.Forms.
  Application`) and a `Color` type (`System.Windows.Media.Color` vs
  `System.Drawing.Color`). Any new file referencing either must qualify it fully
  (`System.Windows.Application.Current`, `System.Drawing.Color.Transparent`) —
  the compiler error (`CS0104`) is clear when this is missed, but it's easy to
  not expect on a WPF project.
- Windows hides a **newly created tray icon** behind the taskbar's `^` overflow
  chevron the first time it appears — this is OS behavior, not a bug in this
  app. There's no supported way to auto-pin it from code.

## Dev control panel (demo mode)

`SimulatedTelemetrySource` implements `IDemoControls`
(`src/IRacingOverlay.Infrastructure/Telemetry/`) — live knobs for exercising the
app without iRacing: add/remove cars, adjust fuel, cycle wetness, add an
incident, toggle the player into the pits. `App.xaml.cs` checks whether the
active telemetry source implements `IDemoControls` and, if so (i.e. `--demo`
was passed), builds a `DevControlViewModel` and shows `DevControlWindow`. In
live mode neither exists — there's nothing to control.

**When adding a new control:** add the method to `IDemoControls`, implement it
in `SimulatedTelemetrySource` under its existing `_gate` lock (the field list,
fuel, wetness, etc. are all mutated from the UI thread via dev-panel button
clicks while the background timer thread reads them every tick — both sides
must lock), wire a `RelayCommand` in `DevControlViewModel`, and add a button to
`DevControlWindow.xaml` using the shared `DevButton` style.

**This code has no automated tests** (see Testing conventions below) and a real
bug shipped here once: `AddCar()` indexed a lookup array using the *current*
field size, which broke as soon as `RemoveCar()` had shrunk the field below its
initial count first. It was only caught by manually soak-testing the running
app. Before trusting a change to this file, write a throwaway console harness
that constructs `SimulatedTelemetrySource` directly and hammers the control
methods in random order for a few thousand iterations, plus the specific
"shrink to the floor, then grow past the original size" case — that's exactly
the shape of bug this file is prone to, and it's cheap to check.

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
   subscribe `window.Closing += HideInsteadOfClose;` (see "Window lifecycle"
   above — skipping this means Alt+F4 on the new window kills the whole app),
   call `.Show()`, and add it to the `TrayIconService` constructor call so it
   gets a "Show <Widget>" menu item.
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

## Releasing

Releases are packaged with [Velopack](https://velopack.io) and published to
**GitHub Releases** by CI. End users download `Setup.exe` from a release and run
it — no .NET runtime or SDK required, because the app is published
**self-contained** (the runtime is bundled).

**Cutting a release** — the version *is* the git tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Pushing a `v*` tag triggers [`.github/workflows/release.yml`](../.github/workflows/release.yml),
which on a `windows-latest` runner publishes self-contained, runs `vpk pack` /
`vpk upload github`, and creates the GitHub Release for that tag. It authenticates
with the built-in `GITHUB_TOKEN` — there are no secrets to configure. Bump the
tag (and, if you want the source tree to match, `<Version>` in
`src/IRacingOverlay.App/IRacingOverlay.App.csproj`) for each release.

**One-time prerequisites:**

- The repo must have a **GitHub remote** and be pushed there
  (`git remote add origin <url>` — this clone may not have one yet), with
  **Actions enabled** for the repo.
- The **vpk CLI version is pinned to the `Velopack` NuGet version** (1.2.0) in the
  workflow. When you bump one, bump the other — a mismatch between the CLI and the
  library is unsupported.

**Entry-point gotcha (why the build is wired the way it is):** Velopack requires
`VelopackApp.Build().Run()` to execute before any UI, so the app provides its own
`Main` (`App.Main` in `App.xaml.cs`) instead of the one WPF generates from
`App.xaml`. That's why the csproj sets `<StartupObject>` and demotes `App.xaml`
from `ApplicationDefinition` to a `Page` (an `ApplicationDefinition` would emit a
competing `Main` and fail to compile). `Main` calls `app.InitializeComponent()`
itself — the step the generated entry point used to do. Don't re-add
`StartupUri`; the composition root in `OnStartup` owns window creation.

**Code signing** is not set up, so Windows SmartScreen shows an "unknown
publisher" prompt on first run (users click *More info → Run anyway*). Fine for a
small team; revisit if the audience grows.

**In-app auto-update** is implemented (`Services/UpdateService.cs`), pointed at
this same public GitHub feed via Velopack's `UpdateManager` — no token needed. On
launch (and from the tray's *Check for updates*) it checks and downloads any newer
release in the background, then reveals a tray *"Restart to install update"* action;
it never restarts on its own. It **only runs for a Velopack-installed copy** —
`UpdateManager.IsInstalled` is false under `dotnet run` or a portable unzip, so
dev/demo launches no-op. Update failures are swallowed (a flaky connection must not
take down the overlay) and logged to `%LocalAppData%\IRacingOverlay\update.log`.
Because the app owns its entry point, the `VelopackApp.Build().Run()` bootstrap in
`App.Main` is what makes the install/update hooks fire — don't remove it.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `dotnet : term not recognized` | SDK not on PATH — see Prerequisites above |
| Build fails on a warning | `TreatWarningsAsErrors` is on by design — fix the warning, don't suppress it |
| Build fails with `MSB3026 ... locked by "IRacingOverlay (pid)"` | A previous run is still alive (it no longer exits when its window closes) — `Get-Process IRacingOverlay \| Stop-Process -Force` |
| `CS0104` ambiguous reference to `Application`/`Color` | `UseWPF` + `UseWindowsForms` both contribute that type name — fully qualify it, see "Window lifecycle" above |
| Demo window never appears | Check the process didn't exit immediately (`echo $?`/exit code) — a startup exception would show as a fast exit |
| Closing a widget window doesn't quit the app | Expected — it hides, not closes. Use the tray icon to bring it back, or its Exit to actually quit |
| No tray icon visible | Windows hides new tray icons behind the taskbar's `^` overflow arrow the first time — click it |
| Live mode stuck on "Waiting for iRacing" | `irsdkEnableMem=1` not set, sim not running, or sim is in exclusive fullscreen |
| A telemetry value is always 0/default in live mode | The SDK variable may not exist on your car/track/build — check it's read via `GetXOrDefault` in `IrsdkTelemetrySource`, not a bare `data.GetX(...)` which would throw |
