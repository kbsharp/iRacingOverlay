# Feature inventory

A detailed record of everything currently implemented, so it can be checked
against instead of re-derived from memory when planning new work. If this
drifts from the code, the code wins — but please update this file in the same
commit as the change that invalidates it.

For setup/build/test commands, see [DEVELOPMENT.md](DEVELOPMENT.md). For the
short pitch and prerequisites, see the [README](../README.md).

## Widgets

### Standings — `StandingsWindow` / `StandingsViewModel` / `StandingsCalculator`

The full, class-grouped field table — the "big" widget, anchored top-left by
default. Every car ordered by position within its class, with best/last lap
times and gaps.

**Layout:** 560px wide, borderless, always-on-top, draggable, right-click →
Exit. Flat, sharp-cornered, near-opaque — styled after RaceLab/iOverlay/LMU
standings. Manual position, top-left (`Left=24, Top=24`); not persisted
between runs. No widget-name label — the class banners and columns identify
it; the top strip carries session type + time/laps remaining + car count.
Under that strip, the column captions sit on a full-bleed **header band**
(`HeaderBand` fill, `Separator` underline) so the table reads as having a head
rather than a floating row of grey labels.

**Rows** are grouped by class. Each class shows a colour-tinted **banner** (a
translucent wash of the sim's `CarClassColor`) with the class short name, its
**Strength of Field** (`StrengthOfField.Compute`, iRacing's real SoF formula),
and a car count. Under it, its cars ordered by position, each with a
full-height class-colour bar flush to the panel's left edge, and alternating
(zebra) row shading. Each car row: class position, car number, driver name, a
license badge and iRating badge (the same tier-coloured chips as the
relative), then **Int** (interval to the car ahead), **Gap** (to the class
leader), **Fastest** (best lap, purple when session-best), and **Last**
(last-lap delta to that car's own best, red when slower). Up to 30 cars per
class are shown (a full class in a typical multiclass split); if the player
falls outside that window, their row is appended so it's always visible.

**Lap times & the fastest-lap highlight:** best/last come from
`CarIdxBestLapTime`/`CarIdxLastLapTime` (formatted `m:ss.fff` by
`StandingsFormat.LapTime`, placeholder when a car has no valid lap yet). The
single fastest valid best lap in the whole field is flagged
`IsSessionBestLap` and rendered in purple, matching iRacing's own timing.

**Gaps & interval** come from `CarIdxF2Time` (a car's time behind the session
leader). The **gap** shown is that value minus the class leader's; the
**interval** is the difference to the car directly ahead in class — both read
correctly regardless of whether F2Time is measured against the overall or
class leader, since they're differences either way. Laps down is derived from
the time gap versus the class leader's best lap (`(int)(gap / leaderBest)`),
**not** a raw completed-lap difference — the latter flickers to "+1L" for a
car only tenths behind whenever the leader has just crossed the line. It falls
back to the completed-lap difference only when no lap time is known.
`StandingsFormat.Gap` renders "+n.n", "+nL" when a lap or more down, or blank
for the class leader / car ahead. **Last** is the last lap minus that car's
own best (`SessionFormat.Delta`, signed).

**In-place updates:** the list is a single flat `ObservableCollection`
(`StandingsViewModel.Items`) of `StandingsRowViewModel` items, each of which
is either a class header or a car row. The collection only changes length
when the field size changes; otherwise every frame updates the existing slots
in place, so position swaps are flicker-free with no per-frame collection
churn.

**Known limitations:**
- Gaps/interval are a transform of `CarIdxF2Time`. In practice and qualifying,
  iRacing reports a best lap time in F2Time rather than a race gap, so those
  columns are only meaningful in race sessions.
- Up to 30 cars per class (`MaxPerClass`), plus the player if outside that.
  Verified rendering a 40-car three-class demo grid; a hypothetical single
  class of 40+ would truncate at 30 (plus player).
- No car-manufacturer logos or iRating ▲/▼ position-change arrows (both shown
  in reference overlays) — the first needs licensed art assets, the second
  needs per-driver start-position tracking. Both are roadmap items.
- Demo-mode gaps are exaggerated (tens of seconds between adjacent positions)
  because the fake F2Time is scaled from the demo's track-position spread;
  real sessions show true sub-second gaps.

### Relative — `RelativeWindow` / `RelativeViewModel` / `RelativeCalculator`

The flagship glance widget: the cars nearest the player on track, ordered
farthest ahead to farthest behind, with a session info strip on top.
Deliberately **compact** — it complements the full standings rather than
duplicating it.

**Layout:** 470px wide, 24px zebra-striped rows, flat and sharp-cornered to
match the standings, borderless, always-on-top, draggable, right-click →
Exit. Manual position, lower-left (`Left=24, Top=760`) so it sits at the
bottom opposite the top-left standings; not persisted between runs. No
widget-name label — the session strip heads it.

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
the layout doesn't jump. Each row shows: race position, a class-colour bar,
car number, driver name, a license badge and iRating badge, a PIT badge when
the car is on pit road or in a pit stall, and a signed time delta (`+n.n` /
`-n.n`). Zebra striping is fixed per slot (`RelativeRowViewModel.IsAltRow`) so
it stays stable as rows update in place.

**Colour coding** (the widget's main visual identity — deliberately not
blue-dominated; blue is reserved for the header label and the A-license
badge specifically):
- **Class bar** (4px, left edge of every row): the car's class colour exactly
  as reported by the sim (`CarClassColor`), not an invented palette. Single-
  class sessions show one colour throughout; multiclass sessions show each
  nearby car's real class colour. `RatingFormat.NormalizeHexColor` converts
  iRacing's decimal-packed `0xRRGGBB` int (its real wire format, e.g.
  `"16750899"` → `#FF9933`) to a CSS-style hex string; a hex-string value is
  also accepted defensively. Unparseable/missing colour falls back to grey.
- **License badge**: a filled chip using iRacing's own license-class colours,
  so it reads instantly to anyone who's played the sim — Rookie red, D
  orange, C yellow, B green, A blue, Pro gold. `RatingFormat.
  ParseLicenseTier` reads the leading letter of the sim's `LicString` (e.g.
  `"B 3.44"` → `LicenseTier.B`).
- **iRating badge**: a filled chip in a separate cool/vivid colour family
  (grey → teal → violet → magenta) so it's never confused with the license
  badge next to it, banded by `RatingFormat.ClassifyIRating`: Low `<1500`,
  Mid `<2500`, High `<4000`, Elite `4000+`.
- Both badges are **tint fill + a 1px edge in the same hue**. The edge is what
  makes them read as chips: at 16px tall a bare tint fill has no boundary and
  just looks like the text is sitting on a smudge. Same treatment on the
  relative's PIT badge and the fuel margin badge.
- **Player row**: a warm amber background wash plus a matching amber border —
  intentionally not the blue accent colour, so "this is you" doesn't visually
  compete with the class/license/iRating colours now on every row.
- Lapped/lapping name colouring (below) and the PIT/wetness amber badges are
  unchanged from before this pass.

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
- Class colouring is per-row, not grouped — there's no separate multiclass
  standings view (that's a distinct roadmap item; the relative always shows
  the nearest cars regardless of class).
- If the player's own car isn't found "in world" for a frame (e.g. between
  sessions), all rows are hidden rather than showing stale data.
- Roster (names/numbers/iRating/license/class) only refreshes when the sim
  re-broadcasts session info; mid-session driver swaps may lag briefly.
- No car manufacturer badge/logo (needs custom art assets) — on the roadmap.

### Fuel — `FuelWindow` / `FuelViewModel` / `FuelCalculator` + `FuelStrategyCalculator` + `LapTimeTracker`

A strategy calculator, not just a burn-rate readout — the numbers shown are
the ones a driver acts on mid-race.

**Layout:** 330px wide, same borderless/transparent/topmost/draggable
behaviour as the relative. Fixed position on first launch (`Left=80, Top=140`
— manual, not persisted). Deliberately compact vertically — every field from
the original layout is still present, just with tighter margins/padding and a
smaller headline number, so it takes noticeably less screen height without
losing any of the strategy numbers.

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
  until both a burn average and a race length are known. Styled as the same
  tint-plus-edge chip as the license/iRating badges, and sharp-cornered
  (`CornerRadius="3"`) — it was the last rounded `8px` pill left over from the
  pre-flat theme.
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

### Setup — `SetupWindow` / `SetupViewModel` / `SetupReminderTracker`

A reminder widget, not a data widget: it exists to catch the "raced on the
low-fuel qualifying setup" mistake — forgetting to load the race setup before
Qualifying or Race starts.

**Layout:** 260px wide, same borderless/transparent/topmost/draggable
behaviour as the others. Fixed position on first launch (`Left=80, Top=470`
— below the fuel widget).

**Displayed fields:**
- The currently loaded setup file name (iRacing's `DriverSetupName`, `.sto`
  extension stripped by `SetupFormat.DisplayName`), shown large.
- A session-type badge (e.g. "RACE", "QUALIFY", "PRACTICE") — amber
  when the session is a Qualify or Race, neutral grey otherwise, so even
  without the flash (below) a glance at the badge tells you whether the
  setup matters right now.
- A "MODIFIED" tag when `DriverSetupIsModified` is set — the loaded setup
  has been changed since it was last loaded/saved.

**The flash:** the whole panel pulses a soft amber wash (`#00FFB03D` ↔
`#70FFB03D`, 0.7s each way, looping) for the first 60 seconds of any session
whose type contains "Race" or "Qualif" (case-insensitive — covers "Race",
"Heat Race", "Qualify", "Open Qualify", "Lone Qualify"). It does **not**
flash for Practice or Warmup. `SetupReminderTracker.Update` is fed every
telemetry frame and detects the session transition by watching
`TelemetrySnapshot.SessionNum` change; the flash window is measured relative
to `SessionTimeSeconds` at that transition, so it's correct whether
`SessionTime` is session-relative or cumulative. Launching the overlay
*during* an already-running Qualify/Race session still flashes immediately
(there's no "already seen it" state carried across app restarts).

Implementation note: the flash animates a `ColorAnimation` targeting the
implicit style-owner element via a property path
(`(Border.Background).(SolidColorBrush.Color)`) rather than
`Storyboard.TargetName` — WPF doesn't allow a `Style`'s triggers to target a
sibling-named brush by name, only the element the style is attached to (or
elements inside a `ControlTemplate`). Verified by sampling the same pixel
from two screenshots ~0.7s apart and confirming the RGB value actually
changed, since a single static screenshot can't prove an animation is
running.

**Known limitations:**
- The 60-second window is a fixed constant, not configurable.
- No acknowledge/dismiss action — the flash simply times out. (Deliberate:
  the request was for a passive visual reminder, not an interactive one.)
- Demo mode starts already in "Race" (matching the existing fuel-strategy
  demo scenario) so the flash is visible from app startup; use the dev
  panel's "Cycle session" button to see Practice → Qualify → Race
  transitions and re-trigger it on demand.

### Radar — `RadarWindow` / `RadarViewModel` / `RadarFormat`

A blind-spot proximity indicator, RaceLab/LMU-style: a small car icon in the
middle with a left and right zone either side of it that light up (and
pulse) when there's a car alongside.

**Layout:** auto-sized, same borderless/topmost/draggable/sharp behaviour as
the others. **No header label** — a radar is self-evident, so the redundant
"RADAR" title was removed; when the sim isn't reporting yet it shows a small
"waiting for spotter data" caption instead. Fixed position on first launch
(`Left=600, Top=470`, right column).

**How it works:** built entirely on iRacing's own `CarLeftRight` telemetry
variable — the exact signal iRacing's built-in spotter uses, not an invented
lateral-position model. `IrsdkTelemetrySource` reads it via
`GetIntOrDefault(data, "CarLeftRight")` and casts to the `CarLeftRight` enum
(`Off`, `Clear`, `CarLeft`, `CarRight`, `CarLeftRight`, `TwoCarsLeft`,
`TwoCarsRight` — mirrors iRacing's own values exactly, confirmed by
reflecting the installed IRSDKSharper package). `RadarFormat`'s pure
classification functions (`HasCarLeft`, `HasCarRight`, `HasTwoCarsLeft`,
`HasTwoCarsRight`, `IsActive`) turn that single enum into the four
independent booleans the widget binds to.

**Visual behaviour:**
- Each side zone pulses a red wash (`#00→#B0FF5C6C`, 0.45s each way,
  looping) continuously while a car is detected there - unlike the setup
  widget's flash, this is **not time-limited**: the hazard indicator only
  stops when the sim reports the car has left the zone.
- A "2" badge appears on a zone for `TwoCarsLeft`/`TwoCarsRight`.
- Both zones can be active simultaneously (`CarLeftRight` value) if there's
  a car on each side.
- Before the sim is actively reporting (`CarLeftRight.Off` — e.g. not yet on
  track), the widget shows a small "waiting for spotter data" caption
  instead of two dim, ambiguous-looking zones.

**Known limitations:**
- `CarLeftRight` is a single aggregate signal, not per-car — the radar can
  tell you *that* someone is alongside and on which side, but not *which*
  car (number/class), or their exact distance. iRacing doesn't expose true
  lateral-offset telemetry per car to compute that honestly; a future
  enhancement could cross-reference the closest car by longitudinal gap
  (the same delta calculation `RelativeCalculator` already does) as a
  best-effort label, but that would be an inference, not a direct read, so
  it's deferred rather than shipped as if it were exact.
- No audio cue — visual only.
- Demo mode's `CarLeftRight` is a fixed `Clear` by default; use the dev
  panel's "Cycle radar" button to step through all six states.

## Telemetry & session data (`Core.Telemetry`, `Core.Session`)

**`TelemetrySnapshot`** — one frame, normalised to the overlay's units
(metres/second, litres, Celsius). Required fields: session time/num/time
remaining/laps remaining, player lap/fuel/speed/gear/on-track flag, player
car index, air/track temp, wetness, brake bias %, incident count,
`CarLeftRight` (near-field proximity, see the Radar widget above), and the
full per-car `Cars` list.

**`CarTelemetry`** — per-car state: car index, lap, lap distance %, `EstTime`,
on-pit-road flag, `CarTrackSurface`, race position, plus the standings fields:
class position, laps completed, best/last lap time, and `F2Time` (time behind
the session leader). iRacing reports non-positive values (typically -1) for
lap times a car hasn't set yet; the calculators treat those as "unknown".

**`CarTrackSurface`** enum mirrors iRacing's `CarIdxTrackSurface`: NotInWorld
(-1), OffTrack (0), InPitStall (1), ApproachingPits (2), OnTrack (3).

**`TrackWetness`** enum mirrors iRacing's `TrackWetness` (0 Unknown through 7
ExtremelyWet).

**`SessionMetadata`** — slow-changing roster data: `DriversByCarIdx`
(`RosterDriver`: car number, display name, iRating, license string,
class-estimated lap time, class short name, raw class colour from the sim),
`SessionTypesByNum`, and the player's own `PlayerSetupName`/
`PlayerSetupIsModified` (drives the Setup widget). Refreshed whenever the sim
re-broadcasts session info. `RelativeRow` carries the same driver fields plus
the parsed `LicenseTier`, `IRatingTier`, and normalised `ClassColorHex` used
for the relative widget's colour coding (see the Relative widget section
above).

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
  `PlayerCarMyIncidentCount`, `CarLeftRight`, and the arrays `CarIdxLap`,
  `CarIdxLapDistPct`, `CarIdxEstTime`, `CarIdxOnPitRoad`,
  `CarIdxTrackSurface`, `CarIdxPosition`, plus the standings arrays
  `CarIdxClassPosition`, `CarIdxLapCompleted`, `CarIdxBestLapTime`,
  `CarIdxLastLapTime`, `CarIdxF2Time`.
- Variables that don't exist on every sim build/car (`AirTemp`,
  `TrackTempCrew`, `TrackWetness`, `dcBrakeBias`,
  `PlayerCarMyIncidentCount`, `CarLeftRight`) go through `GetIntOrDefault`/
  `GetFloatOrDefault` helpers that check `TelemetryDataProperties` first and
  fall back to a default (`CarLeftRight.Off` for the radar) rather than
  throwing. The standings arrays go through guarded `ReadIntArray`/
  `ReadFloatArray` helpers that clear the buffer to zero rather than throwing
  if a variable is absent.
- Session info parsing (`HandleSessionInfo`) filters out spectators
  (`IsSpectator != 0`) and the pace car (`CarIsPaceCar != 0`) when building
  the roster, and reads each driver's `CarClassShortName` and `CarClassColor`
  (iRacing's own per-class colour, normalised by `RatingFormat.
  NormalizeHexColor`) for the relative widget's class colouring.
- Also reads the player's own `DriverInfo.DriverSetupName` and
  `DriverSetupIsModified` for the Setup widget.

**`SimulatedTelemetrySource`** (`--demo`): drives the app without iRacing
running, on a `System.Threading.Timer` ticking at the same ~15Hz as live
mode.
- Builds its field from a selectable **race preset** (`RacePresets`,
  `Core/Demo`) modelled on a real iRacing series — its classes, class colours,
  per-class pace, and a typical grid size. It opens on the IMSA preset (3-class
  GTP/LMP2/GTD); the dev panel's "Cycle race type" button switches to the GT3
  single-class series, the Porsche Cup single-make, or the Mazda MX-5 Cups.
- The field is generated deterministically by `RebuildField(count)`: names,
  numbers, iRatings, and licenses come in order from a 40-name roster pool;
  `DemoFieldPlanner` splits the cars across the preset's classes by share
  (largest-remainder, guaranteeing every class and the player's class a seat).
  Car 0 is always the player. Starting offsets put one car a lap ahead, one a
  lap down, and one parked in the pits — enough variety to see every relative
  widget state at once — and the same builder handles add/remove and race-type
  switches, so there is no separate "reserve roster" path.
- Class colours come from the preset (each hue meaning one class), so the
  relative widget's class-colour bar and the standings class groups have
  something meaningful to show without a live multiclass session.
- Player laps run ~15s so estimates populate within seconds of starting; other
  classes' cadence scales by their relative pace, so a faster class visibly laps
  a slower one.
- For the standings, each car also gets a simulated class position, a
  realistic-looking best/last lap (anchored to its class's base lap via
  `DemoBestLap`, so the table doesn't show silly "0:15" times from the short sim
  laps), and an F2Time derived from its track-position gap to the leader scaled
  by a 100s reference lap. That scaling makes the demo gaps look larger than a
  real race's, but the format and lapped/same-lap behaviour are correct.
- Simulated fuel burn varies per lap (`sin` modulation) so average and
  last-lap figures differ, the way real telemetry does.
- Session is a ~4 minute timed race (see Fuel widget limitations above),
  session type starting at "Race" (`DemoSessions[2]`) to match that scenario.
  The dev panel's "Cycle session" control steps through Practice → Open
  Qualify → Race, each paired with a matching setup file name
  (`practice_setup.sto` / `qualify_setup.sto` / `race_setup.sto`), and bumps
  the session number each time so the Setup widget's flash re-triggers.
- The field is a mutable `List<SimDriver>`, not a fixed array — see
  "Dev experience" below for how it's grown/shrunk live. Also implements
  `IDemoControls`, which the app checks for at startup to decide whether to
  show the dev control panel.
- `CarLeftRight` defaults to `Clear` and is otherwise untouched by the
  simulation loop — it only changes via the dev panel's "Cycle radar"
  control, since deriving a realistic value from the simulated field's
  actual proximity would need lateral-position data the demo doesn't model
  either (see the Radar widget's known limitations above).

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
- Context menu: **Show Standings**, **Show Relative**, **Show Fuel**, **Show
  Setup**, **Show Radar**, a **UI Scale** submenu (100/125/150/175%), **Dev
  Controls** (demo mode only), **Check for updates**, **Exit** — plus a
  **Restart to install update** item that stays hidden until an update has been
  downloaded (see Auto-update below). Double-click the icon = Show Relative.
  **UI Scale** applies a `ScaleTransform` to every overlay window's content root
  (`App.SetScale`); `SizeToContent` then resizes each window to fit, so the whole
  set scales together. Not persisted between runs.
- The app runs under `ShutdownMode="OnExplicitShutdown"`: closing a widget
  window hides it (`App.HideInsteadOfClose`) rather than destroying it, so
  the tray's Show items always work. The tray's **Exit** (or any window's
  right-click **Exit**) is the only path that actually ends the process
  (`App.RequestExit`).
- Windows hides newly created tray icons behind the taskbar's `^` overflow
  arrow by default — expected OS behaviour, not a bug.

### Auto-update — `UpdateService`

Built on Velopack's `UpdateManager`, pointed at the public GitHub Releases feed
the app is published to (`.github/workflows/release.yml`). Public repo, so no
access token.

- On launch — and on demand via the tray's **Check for updates** — it checks the
  feed and, if a newer release exists, **downloads it in the background**. When
  the download is ready it reveals the tray **Restart to install update vX.Y.Z**
  item and shows a balloon notification.
- Applying an update (which restarts the app) only happens when the user clicks
  that item — **never automatically mid-session**, which matters for a racing
  overlay. `App.ApplyUpdate` disposes the tray icon and calls
  `ApplyUpdatesAndRestart`.
- **Installed copies only.** `UpdateManager.IsInstalled` is false under `dotnet
  run` or a portable unzip, so dev/demo launches no-op — no network call, no
  error. Requires the `VelopackApp.Build().Run()` bootstrap in `App.Main`.
- Failures never surface: a flaky connection or bad feed is caught, swallowed,
  and logged to `%LocalAppData%\IRacingOverlay\update.log`. No automated tests —
  it's SDK glue (see Test coverage); the GitHub feed read was verified against
  the live release during development.

### Dev control panel — `DevControlWindow` / `DevControlViewModel` / `IDemoControls`

Shown automatically alongside the other widgets, **only when running with
`--demo`** — it drives `SimulatedTelemetrySource` live, so it has nothing to
control in live mode and doesn't appear there.

| Control | Effect |
|---|---|
| **Cycle race type** | Steps through the demo race presets (`RacePresets`) — IMSA (3-class GTP/LMP2/GTD multiclass), the GT3 single-class series, the Porsche Cup single-make, and the two Mazda MX-5 Cups — wrapping around. Each rebuilds the field with that series' classes, class colours, per-class pace, and a typical grid size, so the standings/relative widgets can be exercised against a single-make grid, a single-class GT3 field, or a full multiclass grid. IMSA is the default the demo opens on. |
| **+ Add car** / **− Remove** | Grows/shrinks the simulated field, 3-40 cars (`MinCarCount`/`MaxCarCount`). The field is rebuilt deterministically from the active race preset for the new size (drivers drawn in order from a 40-name roster pool, split across the preset's classes); removing shrinks by one from the end. |
| **− 5 L** / **+ 5 L** | Adjusts player fuel, clamped to a 65 L tank capacity. Adding fuel mid-lap exercises the same refuel-detection path (`FuelCalculator`'s 0.2 L threshold) that a real pit stop would. |
| **Set critical (2 L)** | Drops fuel straight to 2 L, to check the fuel widget's red "LAPS SHORT" state without waiting for a real burn-down. |
| **Cycle wetness** | Steps through Dry → Very Lightly Wet → Moderately Wet → Very Wet → (wraps to Dry), to check the relative widget's wetness badge. |
| **+ Incident** | Increments the player's incident count shown in the relative session strip. |
| **Toggle player pit** | Flags the player's own row as pitting (surface `InPitStall`), to check the PIT badge and opacity dimming on the player's row specifically. |
| **Cycle session** | Steps Practice → Open Qualify → Race → (wraps), each with its matching setup file, bumping the session number so the Setup widget's flash re-triggers. Resets the "modified" flag, matching a freshly loaded setup. |
| **Toggle setup modified** | Flags the loaded setup as modified, to check the Setup widget's "MODIFIED" tag. |
| **Cycle radar** | Steps Clear → CarLeft → CarRight → CarLeftRight → TwoCarsLeft → TwoCarsRight → (wraps), to check every radar widget state including the pulse and the "2" badge. |

Implementation: `SimulatedTelemetrySource` implements `IDemoControls`
(`src/IRacingOverlay.Infrastructure/Telemetry/`); all mutations happen under
the same lock the background timer thread uses to read state, since
dev-panel clicks (UI thread) and telemetry generation (timer thread) touch
the same fields concurrently.

**A real bug shipped and was caught here:** `AddCar()` originally indexed its
name/number lookup by `currentFieldSize - initialFieldSize`, which goes
negative (→ `IndexOutOfRangeException`, crashing the whole app) once
`RemoveCar()` had shrunk the field below its initial 9. It was caught via
Windows Event Log forensics after a launched instance crashed silently, then
confirmed fixed with a throwaway console harness hammering `AddCar`/`RemoveCar`
in random order for 5000 iterations plus the specific drain-to-floor-then-regrow
shape. That whole incremental-roster path has since been replaced: Add/Remove and
Cycle race type now all funnel through one deterministic `RebuildField(count)`
that regenerates the field from the active preset and a fixed roster pool, so the
class of indexing that crashed is gone by construction. See
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
level), `Temperature` (rounded whole degrees with `°`), `ResolveSessionType`
(looks up and upper-cases the display name for a session number, falling
back to `"SESSION"` — shared by the Relative and Setup widgets).

**`SetupFormat`**: `DisplayName` strips the `.sto` extension from a setup
file name (case-insensitive), or returns the placeholder for a null/blank
name.

**`StandingsFormat`**: `LapTime` (`m:ss.fff`, placeholder when unset/non-
positive) and `Gap` ("+n.n" for a time gap, "+nL" when a lap or more down,
blank for the class leader, placeholder when unknown).

**`RadarFormat`**: classifies iRacing's `CarLeftRight` signal into the
booleans the radar widget binds to - `HasCarLeft`, `HasCarRight`,
`HasTwoCarsLeft`, `HasTwoCarsRight`, `IsActive`.

**`StrengthOfField`** (`Core.Standings`): `Compute(iRatings)` returns iRacing's
real SoF for a class — `B·ln(n / Σ 2^(−ir/1600))`, weighting lower ratings
more heavily than a plain mean; ignores non-positive ratings, 0 for an empty
field.

**`RatingFormat`**: the relative widget's colour-coding logic, kept pure and
testable in `Core` even though the actual brushes live in `App.xaml`.
- `ParseLicenseTier(license)` → `LicenseTier` (Unknown/Rookie/D/C/B/A/Pro) by
  reading the leading letter of the sim's `LicString`.
- `ClassifyIRating(irating)` → `IRatingTier` (Low `<1500` / Mid `<2500` /
  High `<4000` / Elite `4000+`).
- `NormalizeHexColor(raw)` → `"#RRGGBB"` or null. Handles iRacing's real
  `CarClassColor` format (a decimal-packed `0xRRGGBB` int, e.g. `"16750899"`)
  and, defensively, an already-hex value (`"FFCC00"`, `"#ffcc00"`, or an
  8-digit ARGB/RGBA string).

## UI shell (`App.xaml`)

Shared resources used by every window — the single source of truth for the
visual style. Deliberately not blue-dominated: `Accent` is reserved for
branding (window header labels) and the A-license badge specifically; every
other colour carries a distinct meaning (class, license tier, iRating tier,
lap status, "this is you").
- `PanelBackground` — a neutral graphite/charcoal vertical gradient
  (`#1B1D21` → `#121316`) at **~94% alpha** (`F0`): near-opaque, the track
  shows only faintly through, matching the flat/solid look of
  RaceLab/iOverlay/LMU standings. (An earlier ~80% blanket transparency read
  as too see-through and was dialled back.) The material stays a low-
  saturation neutral so colour is reserved for things that carry meaning —
  class, license tier, iRating tier, lap status, "this is you".
- The look is **flat and sharp**, not glassy: panels use a tiny `CornerRadius`
  (~3px, near-square — "ultra modern, not soft"), and the old `PanelSheen`
  (warm glow) and `PanelTopHighlight` (bright edge line) are neutralised to
  near-invisible, kept only as resource keys so existing windows that
  reference them don't need structural edits.
- `RowStripe` — a very faint white (`#0DFFFFFF`) for zebra striping on every
  other row (standings and relative).
- `PanelBorder` — neutral translucent white, not colour-tinted.
- `Separator`, `RowHover`, `HeaderBand` — structural chrome.
- `Accent` (azure blue), `Positive` (green), `Negative` (red), `Warning`
  (amber) — status colours.
- `PlayerHighlight` / `PlayerBorder` — the relative widget's "this is you"
  row wash and outline; warm amber, intentionally not `Accent`, so it doesn't
  compete with the license/iRating/class colours now on every row.
- `LicenseRookie`/`LicenseD`/`LicenseC`/`LicenseB`/`LicenseA`/`LicensePro` —
  iRacing's real license-class colours (red/orange/yellow/green/blue/gold),
  driving the `LicenseBadgeBackground`/`LicenseBadgeText` styles via
  `DataTrigger`s on `RelativeRowViewModel.LicenseTier`.
- `IRatingLow`/`IRatingMid`/`IRatingHigh`/`IRatingElite` — a separate
  grey/teal/violet/magenta family for the iRating badge
  (`IRatingBadgeBackground`/`IRatingBadgeText`), so it's never confused with
  the license badge next to it.
- `TextPrimary`/`TextSecondary`/`TextMuted` — text hierarchy, three clearly
  separated steps. `TextMuted` is deliberately kept well clear of the panel
  material: an earlier dimmer value (`#7C8CAB`) made captions read as grey
  noise rather than as a quiet tier of a hierarchy.
- `LapAheadText` (red-ish) / `LapBehindText` (blue-ish) — relative row
  colouring.
- `FastestLap` (purple) — the standings' session-fastest best lap.
- `FontSmall`/`FontText`/`FontDisplay` — the three Segoe UI Variable **optical
  sizes**, and they are not interchangeable (see Typography below).
- `Caption`, `Value` and `Timing` styles for the small-uppercase-label /
  large-number / tabular-figure patterns used throughout the widgets.
- `DevButton` — flat, rounded button style used by the dev control panel
  (accent-tinted on hover, dimmed when disabled).

### Typography

Segoe UI Variable ships **three optical sizes** and they are not
interchangeable — each is drawn for a size band, so picking the wrong one is a
rendering bug, not a taste call:

| Resource      | Family                      | Use for                          |
| ------------- | --------------------------- | -------------------------------- |
| `FontSmall`   | Segoe UI Variable Small     | ≤11px — captions, badges, PIT    |
| `FontText`    | Segoe UI Variable Text      | 12–28px — window default, rows   |
| `FontDisplay` | Segoe UI Variable Display   | ≥29px — the big fuel readout     |

Every window used to set `Display` as its default and then render 10–13px text
in it. Display is drawn for headlines — thin stems, tight tracking — so at row
size it rendered spindly and washed out ("terminal text"). All windows now
default to `FontText`, with `FontSmall` on captions/badges.

Two things compound this and are why weights here run one step heavier than
they would in a normal window:

- `AllowsTransparency="True"` **disables ClearType**. WPF falls back to
  greyscale antialiasing on layered windows, which thins stems further. There
  is no way to get ClearType back without giving up the transparent overlay.
- Widget text is small and sits over moving scenery.

So: driver names, positions, gaps and captions are `Bold`, not `SemiBold`;
secondary figures are `SemiBold`, not `Normal`. (WPF maps both `SemiBold` and
`DemiBold` to weight 600 — there is no step between 600 and `Bold` 700.)

Numeric fields set `Typography.NumeralAlignment="Tabular"` (via the `Timing`
and `Value` styles, or inline). Without it, proportional figures make ticking
values jitter and right-aligned columns read ragged.

A row's class-colour bar is the one colour that **isn't** a static resource —
it comes from live sim data, so it can't be a fixed set of `DataTrigger`s. The
shared `ClassColorBrush.Resolve` helper (`App/ViewModels/`) parses a row's
normalised hex string into a frozen `SolidColorBrush` (grey fallback on a
parse failure); both the relative and standings rows bind their class bar to
it directly.

All windows share: `DropShadowEffect` for panel lift, a tiny `CornerRadius`,
`BooleanToVisibilityConverter` (`BoolToVis`) for conditional badges, and the
drag-to-move + right-click-exit interaction pattern. Default positions (all
draggable, none persisted yet) are laid out non-overlapping: standings
top-left, relative bottom-left, fuel/setup/radar in a right column, dev
controls far right. UI scale is applied per-window via a `ScaleTransform` on
the content root (see the tray icon section).

## Test coverage

182 xUnit tests, all in `IRacingOverlay.Core.Tests` (the `App` and
`Infrastructure` projects are intentionally not unit tested — see
[DEVELOPMENT.md](DEVELOPMENT.md#testing-conventions)):

| File | Covers |
|---|---|
| `Fuel/FuelCalculatorTests.cs` | Rolling burn average, refuel detection, lap jumps/resets, window trimming |
| `Fuel/FuelStrategyCalculatorTests.cs` | Fuel-to-finish, margin, add-fuel, save target, race-laps estimation (lap-limited and timed) |
| `Fuel/LapTimeTrackerTests.cs` | Rolling lap-time average, jump/reset handling |
| `Relative/RelativeCalculatorTests.cs` | Row ordering, start/finish wrap correction, lap-ahead/behind classification, roster filtering, pit flagging, license/iRating tier and class colour propagation |
| `Standings/StandingsCalculatorTests.cs` | Class grouping/ordering, within-class ordering, class-leader gaps + interval, time-based laps-down (+ lap-count fallback), last-lap delta, per-class SoF, best/last nulls, session-fastest flag, per-class truncation keeping the player, no-metadata fallback, filtering |
| `Standings/StrengthOfFieldTests.cs` | SoF formula (uniform field, empty, non-positive filtering, sub-mean weighting) |
| `Setup/SetupReminderTrackerTests.cs` | Race/Qualify type detection, flash window timing and boundary, session-change restart, first-frame-mid-session behaviour |
| `Formatting/SessionFormatTests.cs` | Time/IRating/delta/wetness/temperature formatting |
| `Formatting/TelemetryFormatTests.cs` | Gear, kph conversion, liters/laps placeholders |
| `Formatting/RatingFormatTests.cs` | License tier parsing, iRating tier boundaries, CarClassColor normalisation (decimal-packed and hex forms) |
| `Formatting/SetupFormatTests.cs` | Setup file name display formatting |
| `Formatting/RadarFormatTests.cs` | CarLeftRight classification into the four proximity booleans |
| `Formatting/StandingsFormatTests.cs` | Lap-time (m:ss.fff) and gap ("+n.n"/"+nL"/blank) formatting |

## Not yet implemented

Tracked in the [README roadmap](../README.md#roadmap):
delta bar, car manufacturer badges (needs custom art assets), drag-to-resize
+ persisted window layout, click-through mode, pinning/auto-showing the tray
icon, running at Windows startup, and a settings surface (units, refresh rate,
widget scale).
