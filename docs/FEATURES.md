# Feature inventory

A detailed record of everything currently implemented, so it can be checked
against instead of re-derived from memory when planning new work. If this
drifts from the code, the code wins — but please update this file in the same
commit as the change that invalidates it.

For setup/build/test commands, see [DEVELOPMENT.md](DEVELOPMENT.md). For the
short pitch and prerequisites, see the [README](../README.md).

## Widgets

### Relative — `RelativeWindow` / `RelativeViewModel` / `RelativeCalculator`

The flagship widget: the cars nearest the player on track, ordered farthest
ahead to farthest behind, with a session info strip on top.

**Layout:** 640px wide, borderless, transparent, always-on-top, draggable
(left-click-drag), right-click → Exit. Centred on screen at first launch
(`WindowStartupLocation="CenterScreen"`); position is not currently persisted
between runs.

**Session strip (top):**
- Session type (from the sim's session-info YAML, e.g. "RACE") + either time
  remaining (`m:ss` / `h:mm:ss`) or laps remaining, whichever the session
  reports — `RelativeViewModel.UpdateHeader`.
- Brake bias (`BB nn.n`) — hidden entirely when the car has no adjustable bias
  (value is 0).
- Track temp / air temp (`TRK n° / AIR n°`).
- Wetness badge — only rendered when the track is at least `VeryLightlyWet`;
  dry conditions show nothing rather than a "DRY" badge.
- Incident count (`Nx`).

**Row list:** fixed 3-ahead / player / 3-behind slots (`slotsPerSide = 3` in
both `RelativeCalculator.Compute` and `RelativeViewModel`). Rows are updated
in place each frame rather than rebuilt, so the list is allocation-free and
the layout doesn't jump. Each row shows: race position, car number, driver
name, license string, iRating (abbreviated to `n.nk` above 1000), a PIT badge
when the car is on pit road or in a pit stall, and a signed time delta
(`+n.n` / `-n.n`).

**Delta calculation:** uses iRacing's `CarIdxEstTime` (the sim's own estimate
of time-to-reach-current-position-on-lap), which is more accurate through
corners than a plain `distance × lap time` estimate. Because `EstTime` resets
at the start/finish line, a raw subtraction breaks for any car on the other
side of the line from the player; `RelativeCalculator.ComputeDelta` detects a
>0.5-lap `LapDistPct` gap and corrects by ±one lap time. Lap time comes from
the roster's `ClassEstLapTimeSeconds` when available, else a 120s fallback
(`FallbackLapTimeSeconds`).

**Lapped/lapping colour coding:** `RelativeCalculator.Classify` compares
total race progress (`lap + lapDistPct`) between the two cars; >0.5 laps
ahead is coloured red (lap-ahead — they're lapping the player), >0.5 behind
is coloured blue (lap-behind — the player is lapping them). A car that has
just crossed the line right in front of the player (lap counter +1, distance
~0) is correctly classified same-lap, not lap-ahead.

**Filtering:** pace cars and spectators are excluded (filtered out of the
roster in `IrsdkTelemetrySource.HandleSessionInfo` before it ever reaches
`RelativeCalculator`); cars not currently "in world" (`CarTrackSurface.
NotInWorld`, e.g. not yet spawned) are excluded per-frame.

**Known limitations:**
- Single-class colouring only — no per-class colour coding yet (multiclass is
  on the roadmap).
- If the player's own car isn't found "in world" for a frame (e.g. between
  sessions), all rows are hidden rather than showing stale data.
- Roster (names/numbers/iRating/license) only refreshes when the sim
  re-broadcasts session info; mid-session driver swaps may lag briefly.

### Fuel — `FuelWindow` / `FuelViewModel` / `FuelCalculator` + `FuelStrategyCalculator` + `LapTimeTracker`

A strategy calculator, not just a burn-rate readout — the numbers shown are
the ones a driver acts on mid-race.

**Layout:** 340px wide, same borderless/transparent/topmost/draggable
behaviour as the relative. Fixed position on first launch (`Left=80, Top=140`
— manual, not persisted).

**Displayed fields:**
- Current fuel level (`nn.nn L`) and laps of running left in the tank at
  current burn (`FuelEstimate.EstimatedLapsRemaining`).
- Used/lap, last lap (both from `FuelCalculator`'s rolling average).
- Race laps remaining (whole laps, from `FuelStrategyCalculator.
  EstimateRaceLapsRemaining`).
- **To finish**: total fuel needed to reach the end of the race
  (`avgLitersPerLap × raceLapsRemaining`).
- **Margin badge**: laps of fuel spare (green) or short (red) at the finish,
  labelled "LAPS SPARE"/"LAPS SHORT". Hidden entirely (`HasStrategy = false`)
  until both a burn average and a race length are known.
- **Add**: litres to add at the next stop to finish with a half-lap safety
  buffer (0 when already enough).
- **Save to**: the burn rate per lap that would still make it to the finish
  on current fuel without stopping — a save target when the driver is short.

**`FuelCalculator`** (per-lap burn): detects lap changes from raw
`(lap, fuelLevel)` frames.
- Rolling window, default last 5 laps (`windowSize`).
- Mid-lap fuel *increase* over 0.2 L (`RefuelThresholdLiters`) is treated as a
  pit stop and invalidates that lap's measurement — small fuel-reading noise
  under that threshold is not.
- A lap-counter jump of more than +1 (missed a lap's telemetry) is not
  recorded — the interval isn't a clean single lap.
- A lap-counter *decrease* (tow back to pits, session restart) re-baselines
  the current-lap tracking but keeps prior recorded laps in the average —
  they're still representative of this car/track/fuel load.

**`LapTimeTracker`** (rolling lap time): same detection pattern as
`FuelCalculator` (rolling window default 5, ignores multi-lap jumps,
re-baselines on lap-counter decrease). Feeds `FuelStrategyCalculator.
EstimateRaceLapsRemaining` for timed races.

**`FuelStrategyCalculator`**:
- `Compute(currentFuel, avgLitersPerLap, raceLapsRemaining, safetyMarginLaps
  = 0.5)` — returns `FuelStrategy.Unknown` until both burn average and race
  length are known (not just "some data" — a null in either input short-
  circuits the whole result).
- `EstimateRaceLapsRemaining` prefers the sim's own lap count for lap-limited
  races (`SessionLapsRemainEx`, sentinel `32767` = unlimited/timed). For
  timed races it derives laps from time remaining ÷ average lap time,
  **rounded up** — the lap in progress must still be completed. Returns null
  for an unlimited/unknown-length session with no usable lap time yet.
- iRacing's "no time limit" sentinel (~604800s / one week) is treated as
  unlimited, matching `SessionFormat.TimeRemaining`.

**Known limitations:**
- No fuel-per-stop split for multi-stop strategies — "Add" assumes one more
  stop covers the rest of the race.
- Safety margin (0.5 laps) is a fixed constant, not user-configurable yet.
- Demo mode always simulates a short (~4 minute) timed race so the margin
  reads comfortably positive; the red "short" state is real code but isn't
  exercised by the demo without editing `SimulatedTelemetrySource`.

## Telemetry & session data (`Core.Telemetry`, `Core.Session`)

**`TelemetrySnapshot`** — one frame, normalised to the overlay's units
(metres/second, litres, Celsius). Required fields: session time/num/time
remaining/laps remaining, player lap/fuel/speed/gear/on-track flag, player
car index, air/track temp, wetness, brake bias %, incident count, and the
full per-car `Cars` list.

**`CarTelemetry`** — per-car state: car index, lap, lap distance %, `EstTime`,
on-pit-road flag, `CarTrackSurface`, race position.

**`CarTrackSurface`** enum mirrors iRacing's `CarIdxTrackSurface`: NotInWorld
(-1), OffTrack (0), InPitStall (1), ApproachingPits (2), OnTrack (3).

**`TrackWetness`** enum mirrors iRacing's `TrackWetness` (0 Unknown through 7
ExtremelyWet).

**`SessionMetadata`** — slow-changing roster data: `DriversByCarIdx`
(car number, display name, iRating, license string, class-estimated lap
time) and `SessionTypesByNum`. Refreshed whenever the sim re-broadcasts
session info.

**`ITelemetrySource`** contract — `TelemetryReceived`, `SessionMetadataReceived`,
`ConnectionChanged`, `ErrorOccurred` events, plus `Start()`/`Stop()`. Events
fire on background threads; all marshalling to the UI thread happens in
`App.xaml.cs`.

## Infrastructure adapters

**`IrsdkTelemetrySource`** (live): wraps IRSDKSharper's `IRacingSdk`.
- Throttles the sim's 60Hz data frames to ~15Hz (`UpdateInterval = 4`) —
  plenty for a human-readable overlay, negligible CPU.
- Reuses fixed-size buffers (`MaxCars = 64`) for the `CarIdx*` array reads
  every frame — no per-frame array allocation.
- SDK variables read: `SessionTime`, `SessionNum`, `SessionTimeRemain`,
  `SessionLapsRemainEx`, `Lap`, `FuelLevel`, `Speed`, `Gear`, `IsOnTrack`,
  `PlayerCarIdx`, `AirTemp`, `TrackTempCrew`, `TrackWetness`, `dcBrakeBias`,
  `PlayerCarMyIncidentCount`, and the arrays `CarIdxLap`,
  `CarIdxLapDistPct`, `CarIdxEstTime`, `CarIdxOnPitRoad`,
  `CarIdxTrackSurface`, `CarIdxPosition`.
- Variables that don't exist on every sim build/car (`AirTemp`,
  `TrackTempCrew`, `TrackWetness`, `dcBrakeBias`,
  `PlayerCarMyIncidentCount`) go through `GetIntOrDefault`/
  `GetFloatOrDefault` helpers that check `TelemetryDataProperties` first and
  fall back to a default rather than throwing.
- Session info parsing (`HandleSessionInfo`) filters out spectators
  (`IsSpectator != 0`) and the pace car (`CarIsPaceCar != 0`) when building
  the roster.

**`SimulatedTelemetrySource`** (`--demo`): drives the app without iRacing
running, on a `System.Threading.Timer` ticking at the same ~15Hz as live
mode.
- Starts with a 9-car field (`InitialField`) with names, car numbers,
  iRatings, licenses, and slightly different lap times, defined so one car
  ends up a lap ahead (D. Whitmore), one a lap down (K. Larsen), and one
  parked in the pits (C. Ibarra) — enough variety to see every relative
  widget state at once.
- Player laps run ~15s so estimates populate within seconds of starting.
- Simulated fuel burn varies per lap (`sin` modulation) so average and
  last-lap figures differ, the way real telemetry does.
- Session is a ~4 minute timed race (see Fuel widget limitations above).
- The field is a mutable `List<SimDriver>`, not a fixed array — see
  "Dev experience" below for how it's grown/shrunk live. Also implements
  `IDemoControls`, which the app checks for at startup to decide whether to
  show the dev control panel.

## Dev experience

### System tray icon — `TrayIconService`

Runs in both live and demo mode. Solves two problems: the widget windows are
borderless/topmost with no taskbar entry (so a stray Alt+F4 or a fullscreen
game can hide one with no obvious way back), and previously the *only* way to
stop the app was closing the terminal that launched it.

- Built on `System.Windows.Forms.NotifyIcon` (the `App` project has
  `UseWindowsForms` enabled alongside `UseWPF` for this — WPF has no native
  tray icon type).
- Icon is drawn at runtime (a navy circle with an azure dot, matching the
  app palette) rather than shipped as an asset file.
- Context menu: **Show Relative**, **Show Fuel**, **Dev Controls** (demo mode
  only), **Exit**. Double-click the icon = Show Relative.
- The app runs under `ShutdownMode="OnExplicitShutdown"`: closing a widget
  window hides it (`App.HideInsteadOfClose`) rather than destroying it, so
  the tray's Show items always work. The tray's **Exit** (or any window's
  right-click **Exit**) is the only path that actually ends the process
  (`App.RequestExit`).
- Windows hides newly created tray icons behind the taskbar's `^` overflow
  arrow by default — expected OS behaviour, not a bug.

### Dev control panel — `DevControlWindow` / `DevControlViewModel` / `IDemoControls`

Shown automatically alongside the other widgets, **only when running with
`--demo`** — it drives `SimulatedTelemetrySource` live, so it has nothing to
control in live mode and doesn't appear there.

| Control | Effect |
|---|---|
| **+ Add car** / **− Remove** | Grows/shrinks the simulated field, 3-20 cars (`MinCarCount`/`MaxCarCount`). Extra cars are drawn from an 11-name reserve roster; removing always drops the most recently added car. |
| **− 5 L** / **+ 5 L** | Adjusts player fuel, clamped to a 65 L tank capacity. Adding fuel mid-lap exercises the same refuel-detection path (`FuelCalculator`'s 0.2 L threshold) that a real pit stop would. |
| **Set critical (2 L)** | Drops fuel straight to 2 L, to check the fuel widget's red "LAPS SHORT" state without waiting for a real burn-down. |
| **Cycle wetness** | Steps through Dry → Very Lightly Wet → Moderately Wet → Very Wet → (wraps to Dry), to check the relative widget's wetness badge. |
| **+ Incident** | Increments the player's incident count shown in the relative session strip. |
| **Toggle player pit** | Flags the player's own row as pitting (surface `InPitStall`), to check the PIT badge and opacity dimming on the player's row specifically. |

Implementation: `SimulatedTelemetrySource` implements `IDemoControls`
(`src/IRacingOverlay.Infrastructure/Telemetry/`); all mutations happen under
the same lock the background timer thread uses to read state, since
dev-panel clicks (UI thread) and telemetry generation (timer thread) touch
the same fields concurrently.

**A real bug shipped and was caught here:** `AddCar()` originally indexed its
name/number lookup by `currentFieldSize - initialFieldSize`, which goes
negative (→ `IndexOutOfRangeException`, crashing the whole app) once
`RemoveCar()` had shrunk the field below its initial 9. Fixed by indexing off
a monotonically-increasing counter instead of the current field size. Caught
via Windows Event Log forensics after a launched instance crashed silently,
then confirmed fixed with a throwaway console harness hammering
`AddCar`/`RemoveCar` in random order for 5000 iterations plus the specific
drain-to-floor-then-regrow shape. See
[DEVELOPMENT.md](DEVELOPMENT.md#dev-control-panel-demo-mode) for why this
file has no permanent automated tests and how to stress-test a change to it
anyway.

### Launch scripts — `scripts/run-demo.ps1`, `scripts/run-live.ps1`

Build the app and start it with `Start-Process` (a genuinely independent
process, not a child of the launching shell), so the terminal can be closed
immediately afterwards without stopping the app — the tray icon is the
control surface from then on.

## Formatting helpers (`Core.Formatting`)

**`TelemetryFormat`**: `Gear` (R/N/1-n), `ToKph` (m/s → rounded km/h),
`Liters` (2dp or a placeholder `–` for null), `Laps` (1dp or placeholder).

**`SessionFormat`**: `TimeRemaining` (`m:ss`/`h:mm:ss`, null for
unlimited/negative), `IRating` (`n.nk` above 1000), `Delta` (explicit-sign
1dp, e.g. `+1.2`/`-0.8`), `Wetness` (short badge text per `TrackWetness`
level), `Temperature` (rounded whole degrees with `°`).

## UI shell (`App.xaml`)

Shared resources used by both windows — the single source of truth for the
visual style:
- `PanelBackground` — a navy-blue vertical gradient (not near-black; a prior
  pass was too dark/desaturated and was corrected).
- `PanelTopHighlight` — a 1px bright line along the top inner edge for a
  glassy look.
- `PanelBorder`, `Separator`, `RowHover`, `HeaderBand` — structural chrome.
- `Accent` (azure blue), `Positive` (green), `Negative` (red), `Warning`
  (amber) — status colours.
- `TextPrimary`/`TextSecondary`/`TextMuted` — text hierarchy.
- `LapAheadText` (red-ish) / `LapBehindText` (blue-ish) — relative row
  colouring.
- `Caption` and `Value` styles for the small-uppercase-label /
  large-number pattern used throughout both widgets.
- `DevButton` — flat, rounded button style used by the dev control panel
  (accent-tinted on hover, dimmed when disabled).

All windows share: `DropShadowEffect` for panel lift, `CornerRadius="16"`,
`BooleanToVisibilityConverter` (`BoolToVis`) for conditional badges, and the
drag-to-move + right-click-exit interaction pattern.

## Test coverage

76 xUnit tests, all in `IRacingOverlay.Core.Tests` (the `App` and
`Infrastructure` projects are intentionally not unit tested — see
[DEVELOPMENT.md](DEVELOPMENT.md#testing-conventions)):

| File | Covers |
|---|---|
| `Fuel/FuelCalculatorTests.cs` | Rolling burn average, refuel detection, lap jumps/resets, window trimming |
| `Fuel/FuelStrategyCalculatorTests.cs` | Fuel-to-finish, margin, add-fuel, save target, race-laps estimation (lap-limited and timed) |
| `Fuel/LapTimeTrackerTests.cs` | Rolling lap-time average, jump/reset handling |
| `Relative/RelativeCalculatorTests.cs` | Row ordering, start/finish wrap correction, lap-ahead/behind classification, roster filtering, pit flagging |
| `Formatting/SessionFormatTests.cs` | Time/IRating/delta/wetness/temperature formatting |
| `Formatting/TelemetryFormatTests.cs` | Gear, kph conversion, liters/laps placeholders |

## Not yet implemented

Tracked in the [README roadmap](../README.md#roadmap): radar/spotter widget,
standings widget, delta bar, multiclass colouring on the relative,
drag-to-resize + persisted window layout, click-through mode, pinning/auto-
showing the tray icon, running at Windows startup, and a settings surface
(units, refresh rate, widget scale).
