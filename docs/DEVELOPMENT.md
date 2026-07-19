# Development guide

Full setup, day-to-day commands, and how to extend the app. For a quick
overview and the widget list, see the [README](../README.md); for what's
implemented in detail, see [FEATURES.md](FEATURES.md).

## Prerequisites

- Windows 10/11
- .NET 8 SDK — `winget install Microsoft.DotNet.SDK.8`
- iRacing (optional — everything below works against `--demo` without it)

The SDK is sometimes installed **per-user** at `%LOCALAPPDATA%\Microsoft\dotnet`
rather than via winget, in which case it isn't on the system PATH. **The scripts
in `scripts/` handle this for you** — `scripts/_common.ps1` finds the SDK (or
fails with a clear message) and every other script dot-sources it. Prefer them
over raw `dotnet` commands, and don't copy a `$env:PATH` fix-up into a new
script or doc — that's the one thing `_common.ps1` exists to keep in one place.

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
  _common.ps1                   # SDK lookup + shared helpers; dot-sourced, not run
  build.ps1                     # build the solution
  test.ps1                      # run Core.Tests (-Filter to narrow)
  run-demo.ps1                  # build + launch, detached from the terminal (--demo)
  run-live.ps1                  # build + launch, detached from the terminal
  render.ps1                    # render widgets offscreen to out/*.png
tools/
  RenderWidget/                 # the offscreen renderer (not in the .sln)
  MakeAppIcon.ps1               # generates Assets/app.ico
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
.\scripts\build.ps1                        # whole solution; warnings are errors
.\scripts\test.ps1                         # Core.Tests, the only project with tests
.\scripts\test.ps1 -Filter FuelCalculator  # narrow to one class while iterating

# Run the app - detached from the terminal (see "Window lifecycle" below)
.\scripts\run-demo.ps1
.\scripts\run-live.ps1
```

Both build/run scripts **stop any running overlay first** (`Stop-RunningOverlay`
in `_common.ps1`). Without that, a rebuild fails with `MSB3026: ... The file is
locked by "IRacingOverlay.Dev (pid)"` — easy to hit, because the app survives
window-close (see below) and a detached launch doesn't visibly tie up a terminal.
This used to be a manual step and no longer is.

To see startup exceptions live, run it tied to the terminal instead — or launch
the built exe directly, which is what a scripted smoke test wants:

```powershell
dotnet run --project src/IRacingOverlay.App -- --demo
src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.Dev.exe --demo
```

There's no watch/hot-reload set up — a rebuild after each change takes a couple
of seconds, so it hasn't been worth adding.

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
  $exe = "src\IRacingOverlay.App\bin\Debug\net8.0-windows\IRacingOverlay.Dev.exe"
  $p = Start-Process -FilePath $exe -ArgumentList "--demo" -PassThru
  Start-Sleep -Seconds 5
  -not $p.HasExited   # True = still running
  Stop-Process -Id $p.Id -Force
  ```

  To actually **see** the rendered UI, render the windows offscreen to PNGs and
  look at those. Don't screen-scrape — the widgets are `ShowInTaskbar="False"` /
  `WindowStyle="None"`, so they have no taskbar entry, can sit behind other
  windows, and capture tooling can't resolve them by name.

  ```powershell
  .\scripts\render.ps1                  # every widget -> out\*.png (~40s)
  .\scripts\render.ps1 fuel relative    # just these two
  ```

  Targets: `standings`, `relative`, `fuel`, `fuel-pit-exit`, `radar`,
  `radar-danger`, `radar-unresolved`, `delta`, `settings`.
  **Rendering everything is the default** and costs barely more than rendering
  one — all the view models are fed from a single demo session, and the slow part
  is wall-clock demo laps (the fuel burn average needs ~35 s of them), not the
  rendering. So after any theme, spacing or typography change, render the lot.

  `radar-danger` is the radar's red proximity glow. It can't be produced from
  demo traffic — the demo field runs nose-to-tail at zero lateral offset, which
  `RadarDanger` correctly reads as queued traffic — so that target drives the
  **spotter fallback** instead, via `IDemoControls.CycleCarLeftRight()`. The glow
  ellipses sit outside both the positional and fallback subtrees in
  `RadarWindow.xaml`, so it's the real binding, not a mock.

  `radar-unresolved` is the other half of that story: it warms up until the track
  *is* mapped, then cycles the spotter to `CarLeft`. The demo field's nose-to-tail
  pack is exactly the stacked-on-the-centreline case, so the render shows what the
  radar does when the geometry can't name a side — faded blips plus a graded glow,
  rather than a confident placement or an empty mirror.

  `fuel-pit-exit` is the fuel widget with its pit-exit projection showing. The
  plain `fuel` target can't produce it, correctly: the demo parks one car in its
  box all session and never cycles anyone through the lane, so `PitLossTracker`
  never sees a pit-road crossing and the strip stays hidden rather than guessing.
  So this target stages the missing input — it takes a warmed-up demo frame and
  replays it with three cars actually crossing in and back out of the lane. Only
  the stops are staged; the tracker, projector and bindings downstream are real.

  **What a render still can't settle** — say so rather than claiming it looks
  right: the *graded* glow (a car fading out as it drifts away) needs real
  side-by-side geometry; whether left/right matches iRacing's live `Yaw` sign;
  and anything about motion or how it reads at racing speed.

  `RenderTargetBitmap` uses greyscale antialiasing exactly as the live
  `AllowsTransparency` windows do, so text weight comes out faithful — the whole
  point when the thing under review *is* the text. This is how the typography
  pass was verified, and how the manufacturer badges were sized (the first pass
  had McLaren's very wide mark collapsing to an invisible hairline — only visible
  by looking).

  The harness is [`tools/RenderWidget`](../tools/RenderWidget), deliberately
  **not** in `IRacingOverlay.sln` so the solution build and CI don't carry it.
  Adding a widget is one entry in `RenderTelemetryWidgets`. Two traps if you
  rewrite it:
  - **Never pump the dispatcher** (no `Show()`, no `Dispatcher.Invoke`).
    Constructing `App` queues `App.OnStartup` on the dispatcher; pumping runs the
    real composition root, which builds `UpdateService` and dies with "No
    VelopackLocator has been set". Drive `Measure`/`Arrange`/`UpdateLayout`
    manually instead — `StaticResource` still resolves via
    `Application.Current.Resources`.
  - It needs the same `Color`/`Brush`/`Size` alias workaround as `App` (see
    CLAUDE.md), because it also sets `UseWindowsForms`.
  - **A window that declares a `Width`/`Height` is measured at that size**, not
    against infinity. The borderless widgets size themselves to their content, so
    infinity is right for them; the settings window is a fixed-size window, and
    measured unconstrained its two columns sprawled to the width of the longest
    hint string — the PNG looked plausible and told you nothing about the real
    window. The declared size is passed down to `RenderToPng`; `NaN` on an axis
    means "no declared size", so that axis still measures against infinity.
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
  `System.Windows.Forms.NotifyIcon` and its context menu (a checkbox per widget,
  UI Scale, Settings..., Dev Controls if present, Exit). This exists because the
  widget windows are borderless/topmost with no taskbar entry — they can get lost
  behind a fullscreen game with no way back except the tray.
- **The settings window is the exception to all of the above.** It's a normal
  resizable window with a taskbar entry (though it draws its own caption via
  `WindowChrome` — see FEATURES), it isn't in `_widgets`, and it genuinely
  closes rather than hiding (it's rebuilt on next open). Its view model subscribes
  to `SettingsService.Changed`, so `SettingsWindow.OnClosed` must call
  `SettingsViewModel.Detach()` — otherwise every open leaks a live handler onto a
  service that outlives the window.
- **Settings changes flow one way:** a control writes to `SettingsService`, which
  raises `Changed`, which `App.ApplySettings` handles by pushing state at every
  widget. Don't have a control apply its own effect *and* save — route everything
  through the service so the tray and the settings window can't drift apart.
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
incident, cycle the raised flag, toggle the player into the pits. `App.xaml.cs` checks whether the
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
   `OverlayViewModelBase`. Override `ApplyTelemetry` (required), plus
   `ApplySessionMetadata` and `ApplySettings` if the widget needs roster data or
   reacts to a user setting. Keep it a thin translation from Core types to display
   strings/bools — no maths here.
5. **Window.** A borderless, transparent, topmost `Window` XAML file styled from
   the shared brushes/styles in `App.xaml` (see [FEATURES.md](FEATURES.md) for
   the current palette). Reuse the drag-to-move and right-click-to-exit pattern
   from `RelativeWindow.xaml.cs` / `FuelWindow.xaml.cs`.
6. **Register it.** Add a constant to `WidgetIds` (`Core/Settings`) and one
   `OverlayWidget` entry to the `_widgets` list in `App.xaml.cs`. That single
   entry is all the wiring there is: the composition root loops over the list for
   telemetry fan-out, `Closing += HideInsteadOfClose`, position restore/tracking,
   scale, visibility and click-through, and `TrayIconService` and the settings
   window both build their per-widget rows from it.

   This is why the registry exists — the same job used to mean editing five
   places, none of which the compiler would flag. Don't reintroduce a
   widget-specific branch in the composition root; if a widget needs something
   the registry can't express, extend `OverlayWidget`.

   **Keep the `WidgetIds` value equal to the window's type name.** Those values
   are the settings-file keys, and the original layout code used type names —
   changing one silently resets every user's saved position for that widget.
7. **Settings, if the widget has tunable numbers.** Put them on `WidgetTuning`
   (`Core/Settings`) with the default equal to the constant the calculator already
   uses, extend `Sanitized()` with a sensible band, add a row to
   `SettingsWindow.xaml`, and read it in the view model's `ApplySettings`. A new
   field must be *additive* — an existing `settings.json` predates it, so absent
   must mean "previous behaviour".
8. **Update the docs:** add the widget to [FEATURES.md](FEATURES.md) and, if the
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

## Dev builds vs the installed app

The two are deliberately kept **entirely separate artifacts**, because most of us
run the released copy for real racing on the same machine we develop on.

|  | Installed release | Source build (Debug) |
|---|---|---|
| Assembly / exe | `IRacingOverlay.exe` | `IRacingOverlay.Dev.exe` |
| Product / Company | `IRacingOverlay` | `iRacing Overlay (Dev)` |
| Version | the git tag, e.g. `0.10.0` | `0.0.0-dev` |
| Settings folder | `%LocalAppData%\IRacingOverlay\` | `%LocalAppData%\IRacingOverlay.Dev\` |
| Settings file | `settings.json` | `settings.json` |
| Single-instance mutex | scoped by install kind | scoped by install kind |

Separate **folders**, not two file names in one folder. The installed folder is
Velopack's — it creates it, updates inside it and deletes it on uninstall — so a
source build writing there left files the uninstaller had no reason to know
about. A dev layout written under the old scheme
(`IRacingOverlay\settings.dev.json`) is copied across on first run by
`SettingsService.MigrateLegacyDevSettings`; it copies rather than moves, since
this build has no business deleting from the installed app's folder. The stale
original is harmless and can be deleted by hand.

The identity split lives in a `Configuration == 'Debug'` property group in
`IRacingOverlay.App.csproj`; **Release builds are untouched**, so the shipped
artifact and the Velopack update feed keep the identity they have always had.

Before this, a Debug build produced an assembly whose identity was byte-identical
to the installed release — same name, product, company and version, down to the
same git hash in `InformationalVersion` — but with different bytes and no
signature. Nothing depended on that being true, and it is a bad thing to be true:
it makes a local build indistinguishable from a tampered copy of the shipped app,
to Windows and to anything else looking. Install-kind detection does **not** key
off the assembly name (it comes from Velopack's `UpdateManager.IsInstalled`), so
renaming is safe.

Note that `scripts/run-demo.ps1` and `run-live.ps1` launch `IRacingOverlay.Dev.exe`;
`Get-Process` and `Stop-Process` during development want that name too.

## Releasing

Releases are packaged with [Velopack](https://velopack.io) and published to
**GitHub Releases** by CI. End users download `Setup.exe` from a release and run
it — no .NET runtime or SDK required, because the app is published
**self-contained** (the runtime is bundled).

### Cutting a release — step by step

**The version *is* the git tag.** Pushing a `v*` tag is the entire release
action; everything else below is making sure the thing you're about to ship to
real users is what you think it is. There is no draft or approval step — the
workflow runs `vpk upload github --publish`, so the release goes **live the
moment the tag lands**, and every installed copy picks it up on its next launch.
Treat pushing the tag as the point of no return.

1. **Be on `main`, up to date, and green.**

   ```powershell
   git checkout main
   git pull
   .\scripts\build.ps1   # TreatWarningsAsErrors is on - a warning is a failure
   .\scripts\test.ps1
   ```

2. **Bump `<Version>` in [`src/IRacingOverlay.App/IRacingOverlay.App.csproj`](../src/IRacingOverlay.App/IRacingOverlay.App.csproj)**
   to the version you're about to tag, minus the `v`. The tag is what CI actually
   builds with (it passes `-p:Version=` from the tag), so the csproj value is the
   source tree agreeing with the tag rather than the thing being released — but
   letting them drift makes `--version` output from a dev build a lie. Semver:
   breaking/major reworks bump minor while pre-1.0, new widgets or settings bump
   minor, fixes bump patch.

3. **Commit the bump and push it.** The tag should point at a commit that's
   already on `main`:

   ```powershell
   git commit -am "chore: bump version to 0.6.0"
   git push origin main
   ```

4. **Sanity-check the actual app once** — `dotnet run --project src/IRacingOverlay.App -- --demo`,
   or better, a live session. Auto-update means a broken release reaches users
   without them choosing to download anything, so a two-minute look at the
   running app is worth more here than anywhere else in this repo.

5. **Tag and push the tag:**

   ```powershell
   git tag v0.6.0
   git push origin v0.6.0
   ```

6. **Watch the run**: <https://github.com/kbsharp/iRacingOverlay/actions> (or
   `gh run watch`). It takes a few minutes.

7. **Verify the release**: <https://github.com/kbsharp/iRacingOverlay/releases>
   should show `iRacing Overlay 0.6.0` with `Setup.exe`, a `.nupkg`, and
   `RELEASES` / `releases.win.json` attached. The last two *are* the update feed —
   without them, installed copies won't see the release.

**What the tag triggers:** [`.github/workflows/release.yml`](../.github/workflows/release.yml)
on a `windows-latest` runner (WPF can't publish elsewhere). It re-runs
`dotnet build`/`dotnet test` in Release first — CI already gates `main`, but a tag
can be cut from any ref and this one publishes straight to users, so it gates
itself independently — then derives the version from the tag (`refs/tags/v0.6.0`
→ `0.6.0`), publishes self-contained win-x64, runs `vpk download github` (pulls
prior releases so Velopack can build a delta — expected to warn and is allowed to
fail on the first release), `vpk pack`, and `vpk upload github --publish`. It
authenticates with the built-in `GITHUB_TOKEN`; there are no secrets to configure.

**If a release goes wrong:** don't delete and re-push the same tag — installed
copies may already have downloaded it, and Velopack's feed doesn't expect a
version to change contents. Bump the patch version and cut a new tag
(`v0.6.1`) instead. Deleting the GitHub *release* (not just the tag) does pull it
from the update feed for anyone who hasn't checked yet, which is the emergency
brake if you catch it fast.

**One-time prerequisites:**

- The repo must have a **GitHub remote** with **Actions enabled** — already the
  case for `kbsharp/iRacingOverlay`; this only matters for a fresh fork or clone.
- **The repo must be public.** `UpdateService` constructs its `GithubSource` with
  `accessToken: null`, so Velopack asks for the release feed unauthenticated —
  against a private repo GitHub answers **404**, the check fails, and the app
  reports itself up to date. That is exactly what happened for 0.4.0–0.6.0: three
  releases published fine and no installed copy could see any of them, with the
  only evidence buried in `update.log`. Making the repo private again would break
  in-app update for every user; distributing an embedded token instead is not a
  fix, it's a published credential.
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
dev/demo launches no-op. Update failures never interrupt a session (a flaky
connection must not take down the overlay) and are logged to
`%LocalAppData%\IRacingOverlay\update.log`; a **manual** check reports them in the
tray, since "couldn't reach the feed" and "you're up to date" must not look the
same to the user.
Because the app owns its entry point, the `VelopackApp.Build().Run()` bootstrap in
`App.Main` is what makes the install/update hooks fire — don't remove it.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `dotnet : term not recognized` | SDK not on PATH — use `scripts\build.ps1`, which finds a per-user install |
| Build fails on a warning | `TreatWarningsAsErrors` is on by design — fix the warning, don't suppress it |
| Build fails with `MSB3026 ... locked by "IRacingOverlay.Dev (pid)"` | A previous run is still alive (it no longer exits when its window closes). `scripts\build.ps1` stops it for you; a raw `dotnet build` doesn't |
| `CS0104` ambiguous reference to `Application`/`Color` | `UseWPF` + `UseWindowsForms` both contribute that type name — fully qualify it, see "Window lifecycle" above |
| Demo window never appears, process exits 0 immediately | A copy of the **same flavour** is already running — `SingleInstanceGuard` yields to it. `Get-Process IRacingOverlay.Dev` to find it; the installed app and a source build don't block each other, two source builds do |
| Process dies instantly with `FileLoadException … Application Control policy has blocked this file (0x800711C7)` | Windows **Smart App Control** has blocked a freshly built binary. It is reputation-based, applies to unsigned local builds, and its verdicts are not predictable from the code — the same project built and ran minutes earlier. **A reboot has cleared it every time so far.** Check with `Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy'` (`VerifiedAndReputablePolicyState`: 0 off, 1 enforcing, 2 evaluation), and confirm the specific file in Event Viewer under `Microsoft-Windows-CodeIntegrity/Operational`. Turning SAC off is possible but **irreversible without reinstalling Windows** |
| Demo window never appears | Check the process didn't exit immediately (`echo $?`/exit code) — a startup exception would show as a fast exit |
| Widgets don't appear at all in live mode | Expected when iRacing isn't running — they stay hidden until telemetry connects. Uncheck *Only show widgets while iRacing is running* in Settings → General to position them with the sim shut |
| Your real layout got reset / dev windows moved your racing layout | Shouldn't happen since the settings split — confirm which file is being written: installed = `%LocalAppData%\IRacingOverlay\settings.json`, everything else = `%LocalAppData%\IRacingOverlay.Dev\settings.json` |
| Installed app never updates | Check `%LocalAppData%\IRacingOverlay\update.log`. A wall of `404 (Not Found)` means the release feed isn't publicly readable — see the release prerequisites above |
| Closing a widget window doesn't quit the app | Expected — it hides, not closes. Use the tray icon to bring it back, or its Exit to actually quit |
| No tray icon visible | Windows hides new tray icons behind the taskbar's `^` overflow arrow the first time — click it |
| Live mode stuck on "Waiting for iRacing" | `irsdkEnableMem=1` not set, sim not running, or sim is in exclusive fullscreen |
| A telemetry value is always 0/default in live mode | The SDK variable may not exist on your car/track/build — check it's read via `GetXOrDefault` in `IrsdkTelemetrySource`, not a bare `data.GetX(...)` which would throw |
