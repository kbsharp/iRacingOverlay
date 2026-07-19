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
Exit. Soft-cornered (`6px`), near-opaque with a top-lit panel material —
styled after RaceLab/iOverlay/LMU standings. Default position top-left
(`Left=24, Top=24`), then restored from
saved settings. No widget-name label — the class banners and columns identify
it; the top strip carries session type + time/laps remaining + car count.
Under that strip, the column captions sit on a full-bleed **header band**
(`HeaderBand` fill, `Separator` underline) so the table reads as having a head
rather than a floating row of grey labels.

**Rows** are grouped by class. Each class shows a colour-tinted **banner** (a
translucent wash of the sim's `CarClassColor`) with the class short name, its
**Strength of Field** (`StrengthOfField.Compute`, iRacing's real SoF formula),
and a car count. Under it, its cars ordered by position, each with a
full-height class-colour bar flush to the panel's left edge, and alternating
(zebra) row shading. Each car row: class position, car number, a
**manufacturer badge**, driver name, a license badge and iRating badge (the
same tier-coloured chips as the relative), then **Int** (interval to the car
ahead), **Gap** (to the class
leader), **Fastest** (best lap, purple when session-best), and **Last**
(last-lap delta to that car's own best, red when slower).

**Manufacturer badge:** each car row carries a badge for the car's make,
derived from the sim's roster. iRacing exposes no manufacturer field — only a
`CarPath` folder token (`ferrari296gt3`) and `CarScreenName` — so
`ManufacturerResolver` (Core) brand-matches those strings to a `Manufacturer`;
an unrecognised car resolves to `Manufacturer.Unknown` and the badge is simply
omitted (the cell collapses for that row, never a placeholder glyph). The badge
is deliberately **neutral/monochrome**, not another coloured tier, so it reads
as iconography in the panel material rather than competing with the class /
license / iRating hues — it takes its colour from the theme, not from the mark.

Marks are the single-path 24×24 glyphs from [Simple Icons](https://simpleicons.org)
(CC0), embedded as WPF path geometry in `ManufacturerMarks` (App) and parsed
once at startup. Two details matter there: every path is prefixed `F1` to
select the **nonzero** fill rule (SVG's default; WPF's mini-language defaults
to even-odd, which renders any mark with a hole — the BMW roundel, the Audi
rings — inverted), and the marks are drawn into a box **wider than it is tall**
(22×14), because several are very wide and flat and collapse to a hairline when
fitted into a square.

Four makes iRacing fields have no CC0 mark upstream — **Dallara, Ligier,
Radical and Ruf** — and fall back to a short brand abbreviation (`DAL`, `LIG`)
in the same chip, so those rows still identify the car. All four are *wordmark*
logos, and a wordmark fitted to the 22×14 badge box gives roughly 3px per
letter — illegible at row size — so the abbreviation is the intended final
rendering rather than a stopgap. **Mercedes** was in this group until its star
was hand-authored: unlike the wordmarks it's pure geometry (a ring plus a
six-vertex star polygon), so it needed no upstream artwork and reads cleanly at
14px. See `ManufacturerBadge` (App) for the mark/abbreviation split.

The per-class cap is **12 cars by default** (`WidgetTuning.StandingsMaxPerClass`,
adjustable 5–60 in Settings); if the player falls outside that window, their row
is appended so it's always visible.

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
- 12 cars per class by default, plus the player if outside that. Verified
  rendering a 40-car three-class demo grid; a single class larger than the
  configured cap truncates (plus player). Adjustable 5–60 in Settings.
- Four makes (Dallara, Ligier, Radical, Ruf) have no CC0 vector mark and show a
  brand abbreviation instead. All four are wordmarks, so the abbreviation is the
  intended rendering, not a stopgap (see the badge section above). McLaren's
  upstream mark is a wordmark too, so it reads denser than the others at row size.
- No iRating ▲/▼ position-change arrows (shown in reference overlays) — needs
  per-driver start-position tracking. A roadmap item.
- Demo-mode gaps are exaggerated (tens of seconds between adjacent positions)
  because the fake F2Time is scaled from the demo's track-position spread;
  real sessions show true sub-second gaps.

### Relative — `RelativeWindow` / `RelativeViewModel` / `RelativeCalculator`

The flagship glance widget: the cars nearest the player on track, ordered
farthest ahead to farthest behind, with a session info strip on top.
Deliberately **compact** — it complements the full standings rather than
duplicating it.

**Layout:** 470px wide, 24px zebra-striped rows, soft-cornered top-lit panel to
match the standings, borderless, always-on-top, draggable, right-click →
Exit. Default position lower-left (`Left=24, Top=760`) so it sits at the
bottom opposite the top-left standings, then restored from saved settings. No
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
  just looks like the text is sitting on a smudge. **On the player row** the
  translucent tint is swapped for an opaque dark backing (an `IsPlayer`
  `DataTrigger`): otherwise the warm amber wash bleeds up through the tint and
  murders the contrast — the hue edge + text still carry the meaning. Same treatment on the
  relative's PIT badge and the fuel margin badge.
- **Player row**: a warm amber background wash plus a warm amber **outer glow**
  (a zero-depth `DropShadowEffect` on the row) so your own line reads as lit, not
  just tinted — the single most important row to find at a glance, in both the
  relative and a full-field standings. Intentionally warm, not the blue accent,
  so "this is you" doesn't compete with the class/license/iRating colours now on
  every row.
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
- No car manufacturer badge here yet — the standings has one (placeholder
  stage); extending it to the relative is a roadmap item.

### Fuel — `FuelWindow` / `FuelViewModel` / `FuelCalculator` + `FuelStrategyCalculator` + `LapTimeTracker`

A strategy calculator, not just a burn-rate readout — the numbers shown are
the ones a driver acts on mid-race.

**Layout:** 330px wide, same borderless/transparent/topmost/draggable
behaviour as the relative. Default position on first launch (`Left=80, Top=140`),
then restored from saved settings. Deliberately compact vertically — every field from
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
- Safety margin defaults to 0.5 laps; configurable 0–5 in Settings.
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
- The flash window defaults to 60 seconds; configurable 5–300 in Settings.
- No acknowledge/dismiss action — the flash simply times out. (Deliberate:
  the request was for a passive visual reminder, not an interactive one.)
- Demo mode starts already in "Race" (matching the existing fuel-strategy
  demo scenario) so the flash is visible from app startup; use the dev
  panel's "Cycle session" button to see Practice → Qualify → Race
  transitions and re-trigger it on demand.

### Radar — `RadarWindow` / `RadarViewModel` + `Core.Radar`

A top-down proximity radar, LMU-style: the player car sits dead centre facing
up, with the nearby field drawn as class-coloured car icons at their real
positions relative to you — **angled to match the track**, so a car alongside
through a corner leans the way the corner does. The whole widget **hides itself
when nobody is near** and reappears the instant a car comes into range.

**Layout:** a fixed 150×240 "scope" (`RadarLayout`) with faint centre axes,
auto-sized panel, same borderless/topmost/draggable/soft-cornered behaviour as the
others. **No header label** — a radar is self-evident. Fixed position on first
launch (`Left=600, Top=470`, right column).

**How it works — reconstructing positions iRacing won't give you.** iRacing's
telemetry exposes each car's `LapDistPct` (how far round the lap it is) but *no*
world position or heading for other cars — only the **player's** heading
(`Yaw`). So the radar learns the track's shape from the player's own driving and
reuses it to place everyone else. Pure, tested logic lives in `Core.Radar`:
- **`TrackMap`** records the player's heading into 720 buckets keyed by
  `LapDistPct`, filling every bucket driven through between samples (so one clean
  lap maps the whole track, not several). A large forward jump — teleport, tow,
  reset — fills only the current bucket, so it can't smear a false line. It
  reports `IsReady` once ≥55% of the lap is mapped (about one lap).
- **`RadarGeometry`** walks that heading table from the player's `LapDistPct` to
  another car's, integrating the track's curve, to recover the car's position
  (`RightMeters`/`ForwardMeters`) **and** orientation (`RelativeAngleRad`) in the
  player's local frame. On a straight the walk is a straight line (cars sit
  directly ahead/behind, parallel); through a corner it bends, so the car ends up
  off to the side and rotated.
- **`RadarCalculator`** builds the `RadarBlip` list for every rostered car within
  range (`DefaultRangeMeters` = 60 m along the track), excluding pit and
  pace/spectator cars, tagging each with its class colour.
- **`TrackLengthParser`** turns iRacing's `WeekendInfo:TrackLength` (`"3.70 km"`,
  occasionally miles) into the metres the geometry needs.

`RadarViewModel` owns the `TrackMap`, feeds it the player's heading each frame
(only while on track and moving >3 m/s), runs the calculator, and maps metres to
canvas pixels in `RadarBlipViewModel` (fixed-size icons, positions scaled at
`PixelsPerMeter`). Blip slots update in place — the collection only resizes when
the number of nearby cars changes.

**Visual behaviour:**
- The panel is visible only when there's something to show: the positional radar
  once the track is mapped and a car is in range, the spotter fallback (below)
  during the first lap, or a small "radar" placeholder before the sim reports (so
  the auto-hiding widget can still be dragged into place).
- Icons are class-coloured (iRacing's own `CarClassColor`); the player car is
  white with an accent nose showing "up".

**First-lap fallback.** Until `TrackMap.IsReady`, there's no shape to place cars
against, so the widget falls back to iRacing's coarse `CarLeftRight` spotter
signal — the same left/right blocks the widget used before this rework, shown via
`RadarFormat`'s `HasCarLeft`/`HasCarRight` classifiers — turning red when a car is
alongside. Once the lap is mapped, the positional radar takes over.

**Known limitations:**
- **Needs ~one lap to learn the track** (per session / track). Before then it's
  the coarse left/right fallback.
- **Lateral offset on a dead straight isn't resolvable.** With no per-car lateral
  telemetry, two cars perfectly side-by-side on a straight both map onto the
  centreline; the geometry separates cars by the track's curvature, which is zero
  on a straight. The angle it *can* show (parallel) is still correct. The spotter
  fallback's left/right remains the honest read for that exact case.
- **Left/right handedness assumes iRacing's `Yaw` is anticlockwise-positive**
  (standard, and what the geometry expects). Worth a live confirmation against the
  sim that a car actually on your left shows on your left — if mirrored, it's a
  one-line sign flip in `RadarBlipViewModel`/`RadarGeometry`.
- No audio cue — visual only.
- Demo mode synthesises a weaving circuit (`SimulatedTelemetrySource.DemoHeading`,
  3000 m) and seats the field in a pack around the player, so the radar populates
  with visibly angled cars; the dev panel's "Cycle radar" button still steps the
  `CarLeftRight` fallback through its states.

## Telemetry & session data (`Core.Telemetry`, `Core.Session`)

**`TelemetrySnapshot`** — one frame, normalised to the overlay's units
(metres/second, litres, Celsius). Required fields: session time/num/time
remaining/laps remaining, player lap/fuel/speed/gear/on-track flag, player
car index, air/track temp, wetness, brake bias %, incident count,
`CarLeftRight` (near-field proximity, see the Radar widget above), and the
full per-car `Cars` list. Also carries `PlayerYawRad` — the player car's
heading (iRacing's `Yaw`), the one heading iRacing exposes, which the radar
records around the lap to reconstruct the track shape.

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
`SessionTypesByNum`, the player's own `PlayerSetupName`/`PlayerSetupIsModified`
(drives the Setup widget), and `TrackLengthMeters` (parsed from
`WeekendInfo:TrackLength`, used by the radar to scale lap-fraction gaps into
metres). Refreshed whenever the sim re-broadcasts session info. `RelativeRow` carries the same driver fields plus
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
- `CarLeftRight` defaults to `Clear` and only changes via the dev panel's
  "Cycle radar" control; it drives the radar's first-lap spotter fallback.
- The demo models a real track shape for the positional radar: `DemoHeading`
  is a weaving 3000 m circuit (`TrackLengthMeters` on the metadata), the
  player's `PlayerYawRad` follows it, and the field is seated in a pack
  straddling the player (cars 1/2 shoved a lap ahead/behind for the relative
  widget's lap-up/lap-down states, the last car in the pits). So the radar
  learns the track within a lap and shows a believably angled pack.

## Dev experience

### System tray icon — `TrayIconService`

Runs in both live and demo mode. Solves two problems: the widget windows are
borderless/topmost with no taskbar entry (so a stray Alt+F4 or a fullscreen
game can hide one with no obvious way back), and previously the *only* way to
stop the app was closing the terminal that launched it.

- Built on `System.Windows.Forms.NotifyIcon` (the `App` project has
  `UseWindowsForms` enabled alongside `UseWPF` for this — WPF has no native
  tray icon type).
- Icon is the shared app mark, `src/IRacingOverlay.App/Assets/app.ico` — see
  *Application icon* below. Loaded at `SystemInformation.SmallIconSize` so a
  scaled display gets the .ico's real 16/20/24/32px drawings instead of a
  resampled 32px bitmap; the runtime-drawn circle it replaced survives as a
  fallback if the resource can't be loaded, because the tray icon is the only
  way to quit the app.
- Context menu: a **checkbox per widget** (Standings, Relative, Fuel, Setup,
  Radar), **Dev Controls** (demo mode only), a **UI Scale** submenu
  (100/125/150/175%), **Settings...**, **Check for updates**, **Exit** — plus a
  **Restart to install update** item that stays hidden until an update has been
  downloaded (see Auto-update below). Double-click the icon = show the Relative.
- The widget items are **checkboxes, not "Show" commands**. A menu that can only
  reveal a widget has no answer to "I don't want the radar"; ticking and unticking
  shows/hides it, and the choice is persisted (`OverlaySettings.EnabledWidgets`),
  so a widget switched off stays off across restarts. The demo-only dev panel keeps
  a plain "show" command — it's scaffolding, not a preference.
- **UI Scale** applies a `ScaleTransform` to every overlay window's content root;
  `SizeToContent` then resizes each window to fit, so the whole set scales
  together. It's the *shared* scale — a widget with its own override (set in the
  settings window) ignores it. The active scale is ticked and **persisted**.
- The menu's checkmarks are re-synced from `SettingsService.Changed`, so toggling
  a widget in the settings window moves the tick here too rather than leaving the
  two surfaces disagreeing.
- The app runs under `ShutdownMode="OnExplicitShutdown"`: closing a widget
  window hides it (`App.HideInsteadOfClose`) rather than destroying it, so
  the tray's Show items always work. The tray's **Exit** (or any window's
  right-click **Exit**) is the only path that actually ends the process
  (`App.RequestExit`).
- Windows hides newly created tray icons behind the taskbar's `^` overflow
  arrow by default — expected OS behaviour, not a bug.
- **Only one copy runs at a time** (`SingleInstanceGuard`, a named mutex claimed
  in `OnStartup` before any window or settings read). A second launch exits
  silently with code 0. Without it, double-clicking the Start-menu entry while the
  app was already running gave two tray icons and two stacks of widgets drawn on
  top of each other — invisible as a *duplicate* precisely because these windows
  have no taskbar entry, so it just looked like the overlay had gone wrong. The
  mutex name is **scoped by install kind**, so a source build and the installed
  app don't block each other: running your dev copy alongside the real one is a
  normal thing to want, and they no longer share a settings file either. An
  `AbandonedMutexException` (previous holder crashed) is treated as "the slot is
  free" — refusing to start until reboot would be worse than the duplicate.

### Application icon — `Assets/app.ico`, `tools/MakeAppIcon.ps1`

A dark rounded panel (the widgets' own material) carrying an azure radar ring
around a warm centre dot — the same accent/warm pairing the overlay uses for
"everyone else" versus "this is you", so the icon reads as the same product as
the widgets rather than generic chrome.

- Used by the **installer, Add/Remove Programs, the Start menu, the taskbar, the
  exe in Explorer** (`<ApplicationIcon>` + `vpk pack --icon`), the **tray**, and
  the **settings window** chrome. Before this the installed app had the stock
  blank-window icon everywhere.
- **Committed as a binary but generated by a script**, `tools/MakeAppIcon.ps1`,
  so the design is editable — the palette and geometry are code, not an opaque
  file nobody can change. Regenerate with
  `powershell -ExecutionPolicy Bypass -File tools/MakeAppIcon.ps1`.
- Each size is **drawn at its own size**, not scaled from one bitmap: the 16px
  entry drops the edge hairline and tightens the corner radius, which a downscale
  would turn to mush.
- Entries at 16–64px are stored as **classic DIB**, 128/256 as **PNG**.
  `System.Drawing.Icon` — which is exactly what `NotifyIcon` uses — cannot decode
  PNG-compressed entries, so an all-PNG icon renders fine in Explorer and throws
  in the tray. The two largest stay PNG because DIB would add ~320KB for no
  visible gain.

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
- Failures are caught and logged to `%LocalAppData%\IRacingOverlay\update.log`;
  they never interrupt a session. A **manual** check (tray → *Check for updates*)
  does report them, though: `CheckAndDownloadAsync` returns an
  `UpdateCheckResult` whose `UpdateCheckStatus` separates `UpToDate` from
  `Failed`, and the tray says *"Couldn't check for updates"* rather than
  *"You're on the latest version"*. Those two were the same `null` return
  originally, which is how a completely broken feed went unnoticed for three
  releases — the repo was private, Velopack asked for the release feed
  unauthenticated, GitHub answered 404, and the app cheerfully reported it was
  current. The repo being public is now a load-bearing assumption of the whole
  update path; if it's ever made private again, in-app update stops working for
  everyone and `update.log` is the only place that says so.
- No automated tests — it's SDK glue (see Test coverage); the feed read was
  verified end-to-end against the live 0.6.0 release, which the installed copy
  downloaded on launch.

### Settings — `SettingsWindow` / `SettingsViewModel` / `Core.Settings`

The user-facing control surface for everything that doesn't fit in a tray menu.
Opened from the tray's **Settings...**; created lazily, since most sessions never
open it.

**It is deliberately a normal window** — standard chrome, a taskbar entry, and no
`AllowsTransparency`. Every other window here is a borderless transparent overlay
because it sits over the sim, but that also costs it ClearType (see Typography).
The settings window is used alt-tabbed, in the pits, so it gets proper subpixel
text rendering and a title bar you can close. Reusing the overlay panel material
here would inherit the greyscale-AA problem for no benefit.

**There is no OK/Apply.** Every control writes straight through to
`SettingsService`, which raises `Changed`, which makes `App.ApplySettings` push
the new state at every widget. The point is watching the overlay react while you
adjust it — a form you fill in and submit would hide exactly the feedback that
makes these numbers choosable.

| Section | Controls |
|---|---|
| **Widgets** | Per widget: on/off, a scale override (100/125/150/175%), and click-through. |
| **Units** | Fuel litres/gallons, temperature °C/°F, speed km/h / mph. |
| **Tuning** | Fuel safety margin (0–5 laps), setup flash (5–300 s), radar range (15–200 m), relative cars each side (1–8), standings cars per class (5–60). |
| **General** | Start with Windows; only show widgets while iRacing is running; **Reset widget positions**. |

- **Per-widget scale** overrides the shared tray scale for that widget only — a
  standings table and a radar rarely want the same size. Absent override = follow
  the shared scale.
- **Click-through** (`WindowInterop.SetClickThrough`, `WS_EX_TRANSPARENT`) makes a
  widget ignore the mouse so clicks reach the sim. It's **per widget, not global**,
  because a click-through widget can't be dragged — the settings window is the only
  way back, so making it all-or-nothing would risk stranding the whole layout.
- **Units convert at format time only** (`Core.Formatting.UnitFormat`). Telemetry
  is normalised to metric on the way in and every calculation stays metric, so the
  unit can be flipped mid-session without invalidating a rolling fuel average or a
  lap-time window. Fuel keeps 2dp and temperature whole degrees in both systems so
  a column doesn't change width when the unit changes. No widget renders a speed
  yet — that preference is here ahead of a readout that uses it.
- **Tuning** feeds the Core calculators, each of which already took the value as a
  parameter. `WidgetTuning`'s defaults are the literal constants those calculators
  used before, so an untouched settings file reproduces the previous behaviour
  exactly. `SetupReminderTracker.FlashDurationSeconds` was the one held as a
  `const` and became a settable property; changing it mid-flash ends the flash on
  the next frame rather than finishing the old window.
- **Reset widget positions** forgets every saved position so each widget returns
  to its default corner next launch — the recovery path for a layout dragged
  somewhere unusable, which previously meant deleting `settings.json` by hand.
- **Start with Windows** writes a per-user `HKCU\...\Run` entry
  (`StartupService`). No elevation needed, and it matches where Velopack installs
  the app. The registered path is `Environment.ProcessPath` — under Velopack the
  stub launcher above `current\`, so the entry survives auto-updates. It persists
  **the state the registry write actually achieved**, not the one requested: a
  locked-down machine can refuse the write, and the checkbox shouldn't then claim
  an autostart entry that doesn't exist. Startup re-asserts the entry if it's meant
  to be on, in case an update moved the executable.
- **Only show widgets while iRacing is running** (on by default,
  `OverlaySettings.HideWhenSimClosed`) keeps every widget hidden until telemetry
  connects, and hides them again when the sim closes. This is what makes *Start
  with Windows* usable: without it, switching autostart on leaves a set of
  always-on-top panels sitting over the desktop for the rest of the day. The rule
  is one pure function — `Core.Settings.WidgetVisibility.ShouldShow(isEnabled,
  isSimConnected, hideWhenSimClosed)` — and it's the single decision point:
  startup, a settings change, and a connect/disconnect all route through
  `App.ShouldShow`, so there's no second path that can put a widget on screen. The
  tray icon stays visible throughout, so the app is never lost. Switch the option
  off to position widgets with iRacing shut; demo mode counts as connected, so
  `--demo` is unaffected.

**Controls are retemplated**, not stock. WPF's default CheckBox / RadioButton /
Slider / ComboBox / ScrollBar render in the system's light chrome, which against
this dark panel reads as a different application pasted into the window. The
`ControlTemplate`s live in `SettingsWindow.xaml`'s own resources rather than
`App.xaml` — the overlay widgets use none of these control types, so a set of
implicit styles in the shared dictionary would be a trap for whoever adds the
first one. Shared palette: `#262D38` fill, `#46525F` edge, `Accent` for the "on"
state.

**Known limitations:**
- Scale is a fixed set of four steps, not free resizing (drag-to-resize is a
  separate roadmap item).
- The window has no automated tests — it's WPF glue (see Test coverage). The model
  underneath it (`OverlaySettings`, `WidgetTuning`, `UnitPreferences`,
  `UnitFormat`) is fully covered. It was reviewed by rendering it offscreen via
  `tools/RenderWidget settings`.

### Layout persistence — `SettingsService` / `Core.Settings`

The UI scale and every widget's window position are remembered between runs, so
the app comes back the way it was left instead of resetting to the default
corners.

- Saved to `%LocalAppData%\IRacingOverlay\` as `OverlaySettings` — **`settings.json`
  for the installed app, `settings.dev.json` for anything else** (`dotnet run`, a
  portable unzip, a build run straight out of `bin\`). `SettingsLocation.FileNameFor`
  picks between them from `UpdateManager.IsInstalled`. They used to share one file,
  which meant a dev session loaded the layout you'd arranged for real racing and
  wrote back wherever the dev windows landed; with both open, each debounce-saved
  the whole file and the last writer won. The installed name is unchanged, so
  existing layouts survive the split — and a dev copy simply starts from defaults
  the first time. Contents:
  the shared `Scale`, a `WindowPosition` per widget, the per-widget
  enabled/scale/click-through maps, `Units`, `Tuning`, `RunAtStartup` and
  `HideWhenSimClosed`. Every
  per-widget map is keyed by `WidgetIds` (whose values are the window type names
  the original layout code used, kept verbatim so existing files still restore)
  and is **sparse**: an absent key means the default, so adding a widget can't
  leave it switched off for existing users and a fresh install writes almost
  nothing.
  That path is in the Velopack install root, above the versioned `current\`
  folder, so it **survives auto-updates** and is removed on uninstall.
- Positions are captured on each window's `LocationChanged` and **debounced**
  (750 ms) so a drag doesn't hammer the disk; a final flush runs on exit and
  before an update-restart. Scale is saved when picked from the tray.
- On launch, `App.RestorePosition` reapplies each saved position **only if it's
  still on a connected display** (`LayoutGuard.IsOnScreen` against the virtual
  desktop), so a layout saved on a since-unplugged monitor falls back to the
  default rather than opening off-screen. The saved scale is applied via
  `App.SetScale` and reflected as a tick in the tray's UI Scale submenu.
- The pure model, serializer (forgiving of a missing/corrupt file), scale
  sanitizing, and on-screen check live in `Core.Settings` and are unit-tested;
  the file I/O + WPF wiring is untested glue (see Test coverage).

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
| **Cycle radar** | Steps Clear → CarLeft → CarRight → CarLeftRight → TwoCarsLeft → TwoCarsRight → (wraps), to check the radar's first-lap spotter fallback (the positional radar itself is driven by the demo track shape, always on once the map is learned). |

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
booleans the radar's first-lap spotter fallback binds to - `HasCarLeft`,
`HasCarRight`, `HasTwoCarsLeft`, `HasTwoCarsRight`, `IsActive`.

**`Core.Radar`** (positional radar): `TrackMap` (learns `heading(LapDistPct)`
from the player's driving), `RadarGeometry` (walks that table to place a car in
the player's local frame — position + angle), `RadarCalculator` (`RadarBlip`
list for cars in range), `TrackLengthParser` (`"3.70 km"` → metres). See the
Radar widget above for how they fit together.

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
  (`#242A34` → `#1A1D23` → `#0E0F13`) at **~94% alpha** (`F0`): near-opaque, the
  track shows only faintly through. The gradient is deliberately wider than a
  flat fill — a lighter, slightly cooler top falling to a deep bottom reads as a
  surface **lit from above**, the material cue that stops the panel looking like
  a flat console rectangle. (An earlier ~80% blanket transparency read as too
  see-through and was dialled back; a still-earlier near-flat `#1B1D21`→`#121316`
  fill read as "terminal".) The material stays a low-saturation neutral so
  colour is reserved for things that carry meaning — class, license tier,
  iRating tier, lap status, "this is you".
- The look has **depth without being glassy**: panels use a `CornerRadius` of
  `6px` (softened from an earlier near-square `3px`), and the once-neutralised
  `PanelSheen` (a faint specular highlight concentrated at the top third) and
  `PanelTopHighlight` (a 1px lit "catch light" along the top edge) are revived to
  sell each panel as a raised object rather than a printed rectangle.
- `RowStripe` — a very faint white (`#0DFFFFFF`) for zebra striping on every
  other row (standings and relative).
- `PanelBorder` — a **top-lit gradient** (bright `#78FFFFFF` white along the top
  edge, dimming to a dark warm-grey down the sides), not a flat hairline: a
  subtle bevel that reads as a physical edge and keeps the widget a distinct
  object over busy track scenery.
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

All windows share: `DropShadowEffect` for panel lift, a `6px` `CornerRadius`,
`BooleanToVisibilityConverter` (`BoolToVis`) for conditional badges, and the
drag-to-move + right-click-exit interaction pattern. Default (first-run)
positions are laid out non-overlapping: standings top-left, relative
bottom-left, fuel/setup/radar in a right column, dev controls far right — but
after that each widget's position is **restored from saved settings** (see
Layout persistence below). UI scale is applied per-window via a `ScaleTransform`
on the content root (see the tray icon section).

## Test coverage

321 xUnit tests, all in `IRacingOverlay.Core.Tests` (the `App` and
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
| `Formatting/RadarFormatTests.cs` | CarLeftRight classification into the four proximity booleans (radar fallback) |
| `Radar/TrackMapTests.cs` | Heading-bucket fill/coverage/readiness, gap-fill between samples (incl. across the line), teleport guard, nearest-bucket lookup |
| `Radar/RadarGeometryTests.cs` | Local-frame placement: straight → ahead/behind at 0°, left/right corners → offset + rotated, reference-heading cancellation, start/finish wrap |
| `Radar/RadarCalculatorTests.cs` | Blip building: map-not-ready/zero-length guards, range gating, pit/pace-car exclusion, roster colour+number |
| `Radar/TrackLengthParserTests.cs` | `WeekendInfo:TrackLength` km/mi parsing, missing/invalid → 0 |
| `Formatting/StandingsFormatTests.cs` | Lap-time (m:ss.fff) and gap ("+n.n"/"+nL"/blank) formatting |
| `Settings/OverlaySettingsSerializerTests.cs` | JSON round-trip (incl. the widget/unit/tuning fields), missing/corrupt file → defaults, out-of-range scale sanitizing, unknown-field tolerance, pre-settings-window file shape still loading with every widget enabled, null maps/records → empty defaults, per-widget scale and tuning clamping |
| `Settings/OverlaySettingsTests.cs` | Sparse-map defaults: absent key = enabled / shared scale / interactive; overrides win; widget ids distinct |
| `Settings/WidgetTuningTests.cs` | Defaults match the previously hardcoded constants, in-band values untouched, out-of-band clamped, non-finite → default not band edge |
| `Settings/UnitPreferencesTests.cs` | Metric defaults, valid choices preserved, undefined enum value → metric |
| `Formatting/UnitFormatTests.cs` | Fuel L/gal, temperature °C/°F, speed kph/mph conversion; placeholders; equal precision across units; agreement with `TelemetryFormat.ToKph` |
| `Settings/LayoutGuardTests.cs` | Scale sanitizing (band + non-finite), on-screen validation across a multi-monitor virtual desktop |

## Not yet implemented

Tracked in the [README roadmap](../README.md#roadmap): delta bar, extending the
manufacturer badge to the relative, drag-to-resize widgets,
a speed readout for the existing km/h / mph preference, a configurable
telemetry refresh rate, per-car/track settings profiles, and pinning the tray icon.

(Click-through, running at Windows startup, and the settings surface itself have
since landed — see [Settings](#settings--settingswindow--settingsviewmodel--coresettings).)
