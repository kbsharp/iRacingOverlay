# Relative widget

`RelativeWindow` / `RelativeViewModel` / `RelativeCalculator`

The flagship glance widget: the cars nearest the player on track, ordered
farthest ahead to farthest behind, with a session info strip on top.
Deliberately **compact** — it complements the full standings rather than
duplicating it.

## Layout

470px wide, 24px zebra-striped rows, soft-cornered top-lit panel matching the
standings, borderless, always-on-top, draggable, right-click → Exit. Default
position lower-left (`Left=24, Top=760`) so it sits opposite the top-left
standings, then restored from saved settings. No widget-name label — the session
strip heads it.

## Session strip (top)

- Session type (from the sim's session-info YAML, e.g. "RACE") + either time
  remaining (`m:ss` / `h:mm:ss`) or laps remaining, whichever the session
  reports — `RelativeViewModel.UpdateHeader`.
- Brake bias (`BB nn.n`) — hidden entirely when the car has no adjustable bias
  (value is 0).
- Track temp / air temp (`TRK n° / AIR n°`).
- Wetness badge — only rendered when the track is at least `VeryLightlyWet`.
  Dry conditions show nothing rather than a "DRY" badge.
- Incident count (`Nx`).

## Row list

Fixed 3-ahead / player / 3-behind slots (`slotsPerSide = 3` in both
`RelativeCalculator.Compute` and `RelativeViewModel`; 1–8 in Settings). Rows are
updated in place each frame rather than rebuilt, so the list is allocation-free
and the layout doesn't jump.

Each row shows: race position, a class-colour bar, car number, driver name, a
license badge and iRating badge, a PIT badge when the car is on pit road or in a
pit stall, and a signed time delta (`+n.n` / `-n.n`). Zebra striping is fixed
per slot (`RelativeRowViewModel.IsAltRow`) so it stays stable as rows update in
place.

## Colour coding

The widget's main visual identity. **Deliberately not blue-dominated** — blue is
reserved for the header label and the A-license badge specifically. Each hue
means exactly one thing.

- **Class bar** (4px, left edge of every row): the car's class colour exactly as
  reported by the sim (`CarClassColor`), never an invented palette.
  `RatingFormat.NormalizeHexColor` converts iRacing's decimal-packed `0xRRGGBB`
  int (its real wire format, e.g. `"16750899"` → `#FF9933`) to a CSS-style hex
  string; a hex-string value is also accepted defensively. Unparseable or
  missing colour falls back to grey.
- **License badge**: a filled chip using iRacing's own license-class colours, so
  it reads instantly to anyone who's played the sim — Rookie red, D orange, C
  yellow, B green, A blue, Pro gold. `RatingFormat.ParseLicenseTier` reads the
  leading letter of the sim's `LicString` (e.g. `"B 3.44"` → `LicenseTier.B`).
- **iRating badge**: a filled chip in a separate cool/vivid family (grey → teal
  → violet → magenta) so it's never confused with the license badge beside it.
  Banded by `RatingFormat.ClassifyIRating`: Low `<1500`, Mid `<2500`, High
  `<4000`, Elite `4000+`.
- **Both badges are tint fill + a 1px edge in the same hue.** The edge is what
  makes them read as chips: at 16px tall a bare tint fill has no boundary and
  looks like text sitting on a smudge.
- **On the player row the translucent tint is swapped for an opaque dark
  backing** (an `IsPlayer` `DataTrigger`). Otherwise the warm amber wash bleeds
  up through the tint and destroys the contrast; the hue edge plus text still
  carry the meaning. Same treatment on the PIT badge and the fuel margin badge.
- **Player row**: a warm amber background wash plus a warm amber **outer glow**
  (a zero-depth `DropShadowEffect` on the row) so your own line reads as lit,
  not merely tinted — the single most important row to find at a glance.
  Intentionally warm rather than the blue accent, so "this is you" doesn't
  compete with the class/license/iRating colours on every row.

## Delta calculation

Uses iRacing's `CarIdxEstTime` (the sim's own estimate of
time-to-reach-current-position-on-lap), which is more accurate through corners
than a plain `distance × lap time` estimate.

Because `EstTime` resets at the start/finish line, a raw subtraction breaks for
any car on the other side of the line from the player.
`RelativeCalculator.ComputeDelta` detects a >0.5-lap `LapDistPct` gap and
corrects by ±one lap time. Lap time comes from the roster's
`ClassEstLapTimeSeconds` when available, else a 120s fallback
(`FallbackLapTimeSeconds`).

## Lapped/lapping colour coding

`RelativeCalculator.Classify` compares total race progress (`lap + lapDistPct`)
between the two cars. More than 0.5 laps ahead is red (they're lapping the
player); more than 0.5 behind is blue (the player is lapping them). A car that
has just crossed the line right in front of the player (lap counter +1, distance
~0) is correctly classified same-lap, not lap-ahead.

## Filtering

Pace cars and spectators are excluded (filtered out of the roster in
`IrsdkTelemetrySource.HandleSessionInfo` before it ever reaches
`RelativeCalculator`). Cars not currently "in world" (`CarTrackSurface.
NotInWorld`, e.g. not yet spawned) are excluded per-frame.

## Known limitations

- Class colouring is per-row, not grouped — the relative always shows the
  nearest cars regardless of class. Class grouping is the standings' job.
- If the player's own car isn't found "in world" for a frame (e.g. between
  sessions), all rows are hidden rather than showing stale data.
- Roster (names/numbers/iRating/license/class) only refreshes when the sim
  re-broadcasts session info; mid-session driver swaps may lag briefly.
- No manufacturer badge here yet — the standings has one. Extending it is a
  roadmap item.
