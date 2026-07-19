# Formatting helpers — `Core.Formatting`

Shared display logic. Anything that turns a number into text belongs here, not
in a view model, and **formats with `InvariantCulture`**.

**`TelemetryFormat`** — `Gear` (R/N/1-n), `ToKph` (m/s → rounded km/h), `Liters`
(2dp, or a placeholder `–` for null), `Laps` (1dp or placeholder).

**`SessionFormat`** — `TimeRemaining` (`m:ss`/`h:mm:ss`, null for
unlimited/negative), `IRating` (`n.nk` above 1000), `Delta` (explicit-sign 1dp,
e.g. `+1.2`/`-0.8`), `Wetness` (short badge text per `TrackWetness` level),
`Temperature` (rounded whole degrees with `°`), `ResolveSessionType` (looks up
and upper-cases the display name for a session number, falling back to
`"SESSION"` — shared by the relative and the fuel widget's setup strip).

**`SetupFormat`** — `DisplayName` strips the `.sto` extension from a setup file
name (case-insensitive), or returns the placeholder for a null/blank name.

**`StandingsFormat`** — `LapTime` (`m:ss.fff`, placeholder when
unset/non-positive) and `Gap` (`"+n.n"` for a time gap, `"+nL"` when a lap or
more down, blank for the class leader, placeholder when unknown).

**`RadarFormat`** — classifies iRacing's `CarLeftRight` signal into the booleans
the radar's first-lap spotter fallback binds to: `HasCarLeft`, `HasCarRight`,
`HasTwoCarsLeft`, `HasTwoCarsRight`, `IsActive`.

**`UnitFormat`** — fuel L/gal, temperature °C/°F, speed kph/mph. Conversion
happens **at format time only**; everything upstream stays metric (see
[settings](settings.md)).

**`RatingFormat`** — the relative widget's colour-coding logic, kept pure and
testable in `Core` even though the actual brushes live in `App.xaml`.

- `ParseLicenseTier(license)` → `LicenseTier` (Unknown/Rookie/D/C/B/A/Pro), by
  reading the leading letter of the sim's `LicString`.
- `ClassifyIRating(irating)` → `IRatingTier` (Low `<1500` / Mid `<2500` / High
  `<4000` / Elite `4000+`).
- `NormalizeHexColor(raw)` → `"#RRGGBB"` or null. Handles iRacing's real
  `CarClassColor` format (a decimal-packed `0xRRGGBB` int, e.g. `"16750899"`)
  and, defensively, an already-hex value (`"FFCC00"`, `"#ffcc00"`, or an 8-digit
  ARGB/RGBA string).

## Related pure logic elsewhere in Core

**`StrengthOfField`** (`Core.Standings`) — `Compute(iRatings)` returns iRacing's
real SoF for a class: `B·ln(n / Σ 2^(−ir/1600))`, weighting lower ratings more
heavily than a plain mean. Ignores non-positive ratings; 0 for an empty field.

**`Core.Radar`** — `TrackMap`, `RadarGeometry`, `RadarCalculator`,
`TrackLengthParser`. See [radar](radar.md) for how they fit together.
