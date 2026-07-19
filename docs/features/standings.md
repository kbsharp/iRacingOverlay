# Standings widget

`StandingsWindow` / `StandingsViewModel` / `StandingsCalculator`

The full, class-grouped field table — the "big" widget. Every car ordered by
position within its class, with best/last lap times and gaps.

## Layout

560px wide, borderless, always-on-top, draggable, right-click → Exit.
Soft-cornered (`6px`), near-opaque with a top-lit panel material. Default
position top-left (`Left=24, Top=24`), then restored from saved settings.

No widget-name label — the class banners and columns identify it. The top strip
carries session type + time/laps remaining + car count. Under it, the column
captions sit on a full-bleed **header band** (`HeaderBand` fill, `Separator`
underline) so the table reads as having a head rather than a floating row of
grey labels.

## Rows

Grouped by class. Each class shows a colour-tinted **banner** (a translucent
wash of the sim's `CarClassColor`) with the class short name, its **Strength of
Field** (`StrengthOfField.Compute`, iRacing's real SoF formula), and a car
count. Under it, its cars ordered by position, each with a full-height
class-colour bar flush to the panel's left edge, and alternating (zebra) row
shading.

Each car row: class position, car number, a **manufacturer badge**, driver name,
a license badge and iRating badge (the same tier-coloured chips as the
relative), then **Int** (interval to the car ahead), **Gap** (to the class
leader), **Fastest** (best lap, purple when session-best), and **Last**
(last-lap delta to that car's own best, red when slower).

The per-class cap is **12 cars by default** (`WidgetTuning.StandingsMaxPerClass`,
adjustable 5–60 in Settings). If the player falls outside that window, their row
is appended so it's always visible.

**In-place updates:** the list is a single flat `ObservableCollection`
(`StandingsViewModel.Items`) of `StandingsRowViewModel` items, each either a
class header or a car row. The collection only changes length when the field
size changes; otherwise every frame updates existing slots in place, so position
swaps are flicker-free with no per-frame collection churn.

## Manufacturer badge

Each car row carries a badge for the car's make, derived from the sim's roster.
iRacing exposes no manufacturer field — only a `CarPath` folder token
(`ferrari296gt3`) and `CarScreenName` — so `ManufacturerResolver` (Core)
brand-matches those strings to a `Manufacturer`. An unrecognised car resolves to
`Manufacturer.Unknown` and the badge is omitted entirely (the cell collapses,
never a placeholder glyph).

The badge is **neutral/monochrome**, not another coloured tier, so it reads as
iconography in the panel material rather than competing with the class /
license / iRating hues. It takes its colour from the theme, not from the mark.

Marks are single-path 24×24 glyphs from [Simple Icons](https://simpleicons.org)
(CC0), embedded as WPF path geometry in `ManufacturerMarks` (App) and parsed
once at startup. Two rules there:

- **Every path is prefixed `F1`** to select the nonzero fill rule. SVG defaults
  to nonzero; WPF's path mini-language defaults to even-odd, which renders any
  mark with a hole — the BMW roundel, the Audi rings — inverted.
- **The box is wider than it is tall (22×14).** Several marks are very wide and
  flat, and collapse to an invisible hairline when fitted into a square.

**Dallara, Ligier, Radical and Ruf** have no CC0 mark upstream and fall back to
a short brand abbreviation (`DAL`, `LIG`) in the same chip. All four are
*wordmark* logos, and a wordmark fitted to the 22×14 box gives roughly 3px per
letter — illegible at row size — so the abbreviation is the intended final
rendering, not a stopgap. Mercedes is hand-authored rather than abbreviated
because its star is pure geometry (a ring plus a six-vertex star polygon) and
needs no upstream artwork. See `ManufacturerBadge` (App) for the
mark/abbreviation split.

## Lap times & the fastest-lap highlight

Best/last come from `CarIdxBestLapTime`/`CarIdxLastLapTime`, formatted
`m:ss.fff` by `StandingsFormat.LapTime` (placeholder when a car has no valid lap
yet). The single fastest valid best lap in the whole field is flagged
`IsSessionBestLap` and rendered in purple, matching iRacing's own timing.

## Gaps & interval

Both come from `CarIdxF2Time` (a car's time behind the session leader). The
**gap** shown is that value minus the class leader's; the **interval** is the
difference to the car directly ahead in class. Both read correctly regardless of
whether F2Time is measured against the overall or class leader, since they're
differences either way.

**Laps down is derived from the time gap versus the class leader's best lap**
(`(int)(gap / leaderBest)`), *not* a raw completed-lap difference. The latter
flickers to "+1L" for a car only tenths behind whenever the leader crosses the
line. It falls back to the completed-lap difference only when no lap time is
known.

`StandingsFormat.Gap` renders "+n.n", "+nL" when a lap or more down, or blank
for the class leader / car ahead. **Last** is the last lap minus that car's own
best (`SessionFormat.Delta`, signed).

## Known limitations

- Gaps/interval are a transform of `CarIdxF2Time`. In practice and qualifying,
  iRacing reports a best lap time in F2Time rather than a race gap, so those
  columns are only meaningful in race sessions.
- 12 cars per class by default, plus the player if outside that. A single class
  larger than the configured cap truncates. Adjustable 5–60 in Settings.
  Verified against a 40-car three-class demo grid.
- Four makes (Dallara, Ligier, Radical, Ruf) show a brand abbreviation — the
  intended rendering, see above. McLaren's upstream mark is a wordmark too, so
  it reads denser than the others at row size.
- No iRating ▲/▼ position-change arrows — needs per-driver start-position
  tracking. A roadmap item.
- Demo-mode gaps are exaggerated (tens of seconds between adjacent positions)
  because the fake F2Time is scaled from the demo's track-position spread. Real
  sessions show true sub-second gaps.
