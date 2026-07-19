# Telemetry, session data & adapters

`Core.Telemetry`, `Core.Session`, `IRacingOverlay.Infrastructure`

This is the page to read when adding a new telemetry field — a new field must be
added to `TelemetrySnapshot` **and** populated by both sources, or demo mode
silently diverges from live.

## Core contracts

**`TelemetrySnapshot`** — one frame, normalised to the overlay's units
(metres/second, litres, Celsius). Fields: session time/num/time remaining/laps
remaining, player lap/fuel/speed/gear/on-track flag, player car index, air/track
temp, wetness, brake bias %, incident count, `CarLeftRight` (near-field
proximity, see [radar](radar.md)), and the full per-car `Cars` list. Also carries
`PlayerYawRad` — the player car's heading (iRacing's `Yaw`), the one heading
iRacing exposes, which the radar records around the lap to reconstruct the track
shape.

**`CarTelemetry`** — per-car state: car index, lap, lap distance %, `EstTime`,
on-pit-road flag, `CarTrackSurface`, race position, plus the standings fields:
class position, laps completed, best/last lap time, and `F2Time` (time behind
the session leader). **iRacing reports non-positive values (typically -1) for
lap times a car hasn't set yet**; the calculators treat those as "unknown".

**`CarTrackSurface`** mirrors iRacing's `CarIdxTrackSurface`: NotInWorld (-1),
OffTrack (0), InPitStall (1), ApproachingPits (2), OnTrack (3).

**`TrackWetness`** mirrors iRacing's `TrackWetness` (0 Unknown through 7
ExtremelyWet).

**`SessionMetadata`** — slow-changing roster data: `DriversByCarIdx`
(`RosterDriver`: car number, display name, iRating, license string,
class-estimated lap time, class short name, raw class colour from the sim),
`SessionTypesByNum`, the player's own
`PlayerSetupName`/`PlayerSetupIsModified` (drives the fuel widget's setup
strip), and `TrackLengthMeters` (parsed from `WeekendInfo:TrackLength`, used by
the radar to scale lap-fraction gaps into metres). Refreshed whenever the sim
re-broadcasts session info.

`RelativeRow` carries the same driver fields plus the parsed `LicenseTier`,
`IRatingTier` and normalised `ClassColorHex` used for the relative widget's
colour coding.

**`ITelemetrySource`** — `TelemetryReceived`, `SessionMetadataReceived`,
`ConnectionChanged`, `ErrorOccurred` events, plus `Start()`/`Stop()`. **Events
fire on background threads**; all marshalling to the UI thread happens in
`App.xaml.cs`.

## `IrsdkTelemetrySource` (live)

Wraps IRSDKSharper's `IRacingSdk`.

- Throttles the sim's 60Hz data frames to ~15Hz (`UpdateInterval = 4`) — plenty
  for a human-readable overlay, negligible CPU.
- **Reuses fixed-size buffers** (`MaxCars = 64`) for the `CarIdx*` array reads
  every frame; no per-frame array allocation.
- SDK variables read: `SessionTime`, `SessionNum`, `SessionTimeRemain`,
  `SessionLapsRemainEx`, `Lap`, `FuelLevel`, `Speed`, `Gear`, `IsOnTrack`,
  `PlayerCarIdx`, `AirTemp`, `TrackTempCrew`, `TrackWetness`, `dcBrakeBias`,
  `PlayerCarMyIncidentCount`, `CarLeftRight`, and the arrays `CarIdxLap`,
  `CarIdxLapDistPct`, `CarIdxEstTime`, `CarIdxOnPitRoad`, `CarIdxTrackSurface`,
  `CarIdxPosition`, `CarIdxClassPosition`, `CarIdxLapCompleted`,
  `CarIdxBestLapTime`, `CarIdxLastLapTime`, `CarIdxF2Time`.
- **Variables that don't exist on every sim build/car must degrade, never
  throw.** `AirTemp`, `TrackTempCrew`, `TrackWetness`, `dcBrakeBias`,
  `PlayerCarMyIncidentCount` and `CarLeftRight` go through
  `GetIntOrDefault`/`GetFloatOrDefault`, which check `TelemetryDataProperties`
  first and fall back to a default (`CarLeftRight.Off` for the radar). Arrays go
  through guarded `ReadIntArray`/`ReadFloatArray` helpers that clear the buffer
  to zero rather than throwing if a variable is absent.
- Session info parsing (`HandleSessionInfo`) filters out spectators
  (`IsSpectator != 0`) and the pace car (`CarIsPaceCar != 0`) when building the
  roster, and reads each driver's `CarClassShortName` and `CarClassColor`
  (normalised by `RatingFormat.NormalizeHexColor`).
- Also reads the player's own `DriverInfo.DriverSetupName` and
  `DriverSetupIsModified` for the setup strip.

## `SimulatedTelemetrySource` (`--demo`)

Drives the app without iRacing running, on a `System.Threading.Timer` ticking at
the same ~15Hz as live mode.

- Builds its field from a selectable **race preset** (`RacePresets`,
  `Core/Demo`) modelled on a real iRacing series — its classes, class colours,
  per-class pace, and a typical grid size. It opens on the IMSA preset (3-class
  GTP/LMP2/GTD); the dev panel's **Cycle race type** switches to the GT3
  single-class series, the Porsche Cup single-make, or the Mazda MX-5 Cups.
- The field is generated deterministically by `RebuildField(count)`: names,
  numbers, iRatings and licenses come in order from a 40-name roster pool;
  `DemoFieldPlanner` splits cars across the preset's classes by share
  (largest-remainder, guaranteeing every class and the player's class a seat).
  Car 0 is always the player. Starting offsets put one car a lap ahead, one a lap
  down, and one parked in the pits — enough variety to see every relative widget
  state at once. **Add/remove and race-type switches all funnel through this one
  builder**; there is no separate incremental-roster path (see
  [dev-tools](dev-tools.md) for why that matters).
- Class colours come from the preset, so the class-colour bar and standings
  class groups have something meaningful to show without a live multiclass
  session.
- Player laps run ~15s so estimates populate within seconds; other classes'
  cadence scales by their relative pace, so a faster class visibly laps a slower
  one.
- For the standings, each car gets a simulated class position, a
  realistic-looking best/last lap (anchored to its class's base lap via
  `DemoBestLap`, so the table doesn't show silly "0:15" times from the short sim
  laps), and an F2Time derived from its track-position gap to the leader scaled
  by a 100s reference lap. That scaling makes demo gaps look larger than a real
  race's, but the format and lapped/same-lap behaviour are correct.
- Simulated fuel burn varies per lap (`sin` modulation) so average and last-lap
  figures differ, the way real telemetry does.
- Session is a ~4 minute timed race, session type starting at "Race"
  (`DemoSessions[2]`). **Cycle session** steps Practice → Open Qualify → Race,
  each paired with a matching setup file name (`practice_setup.sto` /
  `qualify_setup.sto` / `race_setup.sto`), bumping the session number so the
  setup reminder's flash re-triggers.
- `CarLeftRight` defaults to `Clear` and only changes via the dev panel's
  **Cycle radar** control; it drives the radar's first-lap spotter fallback.
- Models a real track shape for the positional radar: `DemoHeading` is a weaving
  3000 m circuit (`TrackLengthMeters` on the metadata) and the player's
  `PlayerYawRad` follows it, so the radar learns the track within a lap.
- Implements `IDemoControls`, which the app checks for at startup to decide
  whether to show the dev control panel.
