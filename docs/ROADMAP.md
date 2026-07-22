# Roadmap

What's planned, what's deliberately not, and the judgement behind both. For what
already exists — every field and calculation — see [FEATURES.md](FEATURES.md).
The July 2026 deep-dive audit — the honest state of the product, the accuracy
and accessibility reviews, and the market check behind the corrections woven in
below — is [AUDIT-2026-07.md](AUDIT-2026-07.md).

## The two tests

Every candidate feature has to pass both.

> **1. Does it change a decision the driver makes in the car?**

If it doesn't, it's out, however good it looks on a feature grid. Heart-rate
monitors, G-force meters and boost boxes are decoration; a number that says *pit
now, not next lap* is the product.

> **2. Is it in a unit the driver already has, and can they tell when it's wrong?**

Not a rule against forecasts. "Closing 0.3s/lap, contact in 1 lap" is a
prediction and it's fine — seconds and laps are units every driver owns, and a
lap later you can see whether it was right. Same as "60% chance of rain":
legibly uncertain, in units you understand, checkable after the fact. What fails
is a number you must be *taught* before it says anything (`49 CPI`), or one
measured against a baseline only this app can see. **Uncertainty is fine;
illegibility isn't.**

## The focus

- **RaceLab** is a platform: 29 overlays, 80+ data blocks, 7 sims, VR, layout
  library, Stream Deck, streaming tools. Breadth is its game.
- **iOverlay** is iRacing-only like us, and sells customization: 21 standings
  columns, ~32 header values, pick your own.
- **irDashies** is the free, open-source flank the first snapshot missed: 20+
  widgets, drag-to-resize with snapping, driver tagging, weather, a rejoin
  indicator — shipping fast, for nothing. It gives customization away free,
  which is why competing on customization — or on "free" alone — is a fight
  with no prize.
- **CrewChief** is the free *voice* race engineer half the grid already runs.
  It overlaps our strategy layer (spoken pit-exit predictions on request, from
  a practised benchmark stop) without drawing anything.
- **This app** sells judgement: iRacing-only, opinionated, zero-config, *right by
  default*. A strong core — six widgets finished properly plus the strategy
  layer no other overlay draws — not a wide catalogue. **A race engineer, not a
  dashboard.**

## Next up

Reordered by the [July 2026 audit](AUDIT-2026-07.md) — first-impression and
accessibility items first:

- **Defaults pass** — the track map joins the delta in `DefaultOffWidgets`:
  it's the least decision-dense panel and the only default-on widget that
  doesn't hide itself, and its constituency knows to want it (one tray click).
  Standings + relative + fuel is the right first impression; the radar stays
  on because a self-hiding widget costs nothing. Same pass fixes the
  first-run overlap: the fuel widget's default `600,24` sits ~40px over the
  standings' right edge until dragged.
- **Colour-blind friendly palette** — one preset, not per-deficiency modes:
  gain/loss moves off the green/red axis (near-identical luminance under
  deutan/protan vision), fastest-lap purple and the lapped/lapping tints get
  re-picked, the radar glow moves to a protan-bright hue. Sim-supplied class
  and license colours stay — the license chip already carries its letter.
  Every meaning-hue is single-sourced in `App.xaml`, so this is design work
  plus a resource swap. Roughly 8% of a male-skewed user base is red-green
  colour-blind, and no mainstream overlay ships this.
- **Drag-to-resize** — promoted from "carried-over polish": it's the largest
  daily-feel gap against every competitor, paid and free. Resize is not
  customization; it's making one opinionated layout fit any monitor.
  (Tray-icon pinning stays carried over.)
- **Multi-stop honesty on the fuel widget** — "Add" assumes one more stop
  covers the race, so in a 2+-stop race it can name more litres than the tank
  holds. Cap it at capacity and say "+1 stop" when that's the truth.
- **Weather forecast strip** *(mid-term)* — we show current temps and wetness;
  with dynamic weather the decision-shaped question is "what's it doing in 20
  minutes?", which is a tyre and pit call. irDashies shipping weather widgets
  confirms the demand; ours stays a forecast, not current-conditions wallpaper.
- **Per-session-type profiles** *(mid-term)* — the practice layout genuinely
  isn't the race layout. Grow the settings-profile idea with a session-type
  dimension. This — not column pickers — is the customization axis that fits
  the product. The audit also blessed **subtractive content toggles** (first
  candidate: hide the ratings chips, for drivers who race better without
  rating anxiety on screen); toggles remove what a driver doesn't act on,
  never rearrange what remains.
- **Sign the installer** *(distribution, when ambitions firm up)* —
  SmartScreen's "unknown publisher" is a trust tax on every download. The
  blocker is that certificates cost real money, not engineering.

## Needs research first

Passes the decision test, but an open question decides whether there's a feature
at all — so the research comes before the build.

- **Manufacturer badge on the relative.** It already ships on the standings, off
  by default, and moving it looks like half an hour of markup. Two things say
  otherwise. **(1)** `ManufacturerResolver` isn't exhaustive; an unrecognised car
  resolves to `Unknown` and the column is blank. On a long standings list that
  reads as gaps; on the relative's six rows, a blank next to the car you're
  fighting is the one place the badge had a job. What fraction of a real
  relative's rows resolve, across the classes people actually race? **(2)** The
  relative already colours by `CarClassColor`, and in a single-make field every
  badge is identical — a column of the same mark, costing width on the densest
  row in the app. Does it change a decision *there*, or is it the standings
  feature relocated? Answer both with real multiclass sessions, not reasoning.
- **Rejoin indicator and slow-car-ahead warning.** Both pass the decision test
  hard — they prevent the two worst incident types, the blind rejoin and the
  closing-speed pile-up — and irDashies ships both, so there is prior art to
  study rather than guess at. The research questions: what signal quality does
  iRacing's telemetry actually support for "safe gap to rejoin into" (we
  already place every car by lap fraction and know the player's off-track
  state), and does a *visual* warning beat the audio spotter/CrewChief calls
  most drivers already run — or complement them for the drivers who can't use
  audio? Study both in real traffic before designing anything.

## Open questions only a real session can answer

Renders and tests can't settle these; they need a human eye at racing speed.

- **Radar range** — the default dropped 60 m → 40 m when the scale started
  following the range. Does 40 m feel right, and does a car right on your
  gearbox (now overlapping the player mark by about half a blip) read clearly?
- **Pit-exit strip** — does it earn its space for the whole race, or only near a
  stop? It's live from the third observed stop onward, which is always-true
  information but not always a live decision.
- **Traffic forecast** — do the three-lap horizon and the "this lap / next lap /
  in N laps" granularity feel right against the gaps a real multiclass field
  produces, rather than the demo's tight pack?
- **Track map fidelity** — the outline is the *player's line*, not a surveyed
  centreline, and it is drawn from whatever laps you happened to drive. Does a
  real circuit come out recognisable, or does a wide moment leave a corner shaped
  wrong until it's driven again? And is a 208 px square enough to place a rival
  at a glance on a long track like Spa, where the whole lap has to fit?
- **Push-or-save strip** — does a driver's natural burn variation reach the 6%
  spread the fit requires often enough for the strip to appear when it's wanted?
  If it usually needs you to have *already* been saving, the thresholds want
  revisiting.

## Parked — good, but not core

Real features both competitors ship, which fail the in-car decision test. They
serve self-review and streaming, not the driver mid-race. Revisit once the core
is strong.

- **Input telemetry trace** (throttle/brake/steering).
- **Lap-time log/graph** (stint review).
- **Speed readout** — the car's own dashboard has a speedometer a glance away, so
  a dedicated panel changes no decision. Cheap to add if a specific use turns up
  (a pit-limiter cue *would* be a decision); as a bare speed block, parked.

## Non-goals — on purpose

- **VR.** Off the table. (It would also mean a second rendering stack — WPF
  layered windows can't reach an HMD — but the reason is focus, not difficulty.)
- **Gimmick data blocks** — heart rate, G-force, boost box, overtake flashes.
- **Full column-level customization.** The paid competitors sell it hard and
  irDashies gives it away free — which settles it: a customization race has no
  prize, and it was our anti-feature anyway. Every column here earns its place
  and **the default *is* the product**. Subtractive show/hide toggles are fine;
  a layout editor is not.
- **Streaming toolkit** (Twitch chat, garage cover, scene tools, Stream Deck). A
  different product for a different user.
- **Team fuel sharing.** Needs accounts, a backend and invite management.
- **Series/stats module, replay, driver tagging, tools manager.** Platform
  features for the *between-races* user; this app serves the *in-the-car* one.

## What shipped, and what it taught

Landed — the reasoning that survives is in [FEATURES.md](FEATURES.md):

| Shipped | The call behind it |
|---|---|
| **Standings pit status** | PIT takes over the Int column — Int is meaningless for a car in its box; Gap, which says where they rejoin, is kept. |
| **Delta bar** | One reference (your session best), not a chooser — one number is the product. Holds the finished lap's figure 5s at the line, where the sim zeroes it exactly as it becomes worth reading. **Off by default**: it restates the sim's own black box, so it earns a standing panel only for drivers who want it always up. |
| **Radar density pass** | Turned out to be a defect, not a spacing question: `PixelsPerMeter` was constant while range was a setting, so the canvas and the range disagreed everywhere. Scale now derives from range; the default range *is* the density control. |
| **Radar unresolved geometry** | Lateral offset is inferred, so a car level with you lands on the centreline. A blip drawn closer than a car's width inside the overlap box is a position no two cars can occupy — the geometry saying *I don't know*. It defers to iRacing's `CarLeftRight` and dims, rather than drawing inference as confidently as measurement. |
| **Configurable refresh rate** | The offered rates are exactly the divisors of iRacing's 60 Hz broadcast, so the number shown is the number delivered. A free-form slider would round and then lie. |
| **Pit-exit projection** | Lane length isn't published, so the cost is *measured* — off the whole field's stops, because a figure that waits for your own first stop arrives after the decision. The strip states the loss it used, so it's falsifiable rather than oracular. |
| **Traffic forecast** | Gap from the relative's own `EstTime` delta, closing rate from the sim's per-class pace, meeting point named by timing sector. Self-hides in single-class racing and beyond three laps — a warning you can't act on is noise. |
| **Track map** | The gap both competitors filled with a track database, filled instead by walking the shape the radar already learns: a heading plus a distance is a step, so a lap of steps *is* the outline. No database means no missing circuit and nothing stale after a resurface — and the first lap, which the widget spends saying how much it has learned rather than drawing half a track. Cars are class-coloured dots and nothing else; at map scale a field of numbered marks is mush. |
| **Push-or-save tradeoff** | Both ways out priced in seconds. What saving costs is regressed from the driver's own laps, and the fit is refused more often than it's offered (sign checked, outliers dropped, no reading past the observed burn range). No verdict: two numbers in the same unit are the sentence. |

Built and then withdrawn — these two are the live guidance:

- **Catch/defend pace trend** — *shipped, now off by default behind
  `ShowPaceTrend`.* The maths passes both tests; the **presentation** fails the
  second. On the row it's a bare `▲ 0.2` with no unit and no referent, sat
  between an iRating badge and a delta — one more number to decode. Parked behind
  a toggle rather than deleted because the path is **fixable**: carry the unit or
  referent on the row (`-0.4s/L`, or a form that reads as *closing*), or drop the
  rate and keep only the laps-to-contact countdown, which needs no teaching.
  Worth resolving against the delta column too — two signed numbers side by side
  is the density problem, not just the label.
- **Projected Safety Rating chip** — *built, then removed.* It shipped as
  corners-per-incident (`▲ 49 CPI`) because iRacing has never published the
  CPI-to-SR conversion. That reasoning held, and it was the problem: CPI isn't a
  unit drivers think in, and the arrow was measured against a baseline only this
  app knew. Deleted rather than toggled off, because unlike the badge and the
  pace trend it had no fixable path, so the toggle would have been permanent —
  the hedge "the default *is* the product" exists to avoid. The whole
  implementation is intact at commit `7abef1b` and returns with a cherry-pick if
  a better unit ever turns up.

The rule those two settle: **a toggle is a staging area for something fixable,
not a way to keep something that doesn't work.**

## Market snapshot (July 2026)

Corrected by the [July 2026 audit](AUDIT-2026-07.md): **irDashies** (free,
open source) added — the first snapshot compared the paid tools only — and two
uniqueness claims narrowed after checking them against the world. `·` = not
verified. **CrewChief** sits outside the table (it draws nothing) but overlaps
the strategy layer by voice and is free — the pit-exit row says how.

| Capability | RaceLab | iOverlay | irDashies | Us |
|---|---|---|---|---|
| Standings / relative / fuel | ✅ | ✅ | ✅ | ✅ |
| Positions gained vs the grid | ✅ | ✅ (column) | · | ✅ per class |
| Proximity radar | ✅ (radar + bars) | spotter indicator | ✅ blind-spot monitor | ✅ corner-angled, learned geometry |
| Track map | ✅ | ✅ (2 forms) | ✅ (2 forms) | ✅ learned from your own driving, **no track DB** |
| Delta bar | ✅ | via columns | ✅ (+ sector delta) | ✅ vs your session best, held at the line |
| Projected iRating | gain shown | gain shown | · | ✅ full zero-sum model |
| Pit-exit position projection | ❌ | ❌ | ❌ | ✅ **only overlay that draws one** — learned lane cost, stated on the strip. CrewChief *speaks* an estimate on request, from a benchmark stop practised in advance; ours is field-learned, continuous, visual |
| Traffic meeting-point forecast | ❌ | behind-only warning | behind-only warning | ✅ **(unique)** — names the lap *and* sector it reaches you |
| Push-or-save fuel tradeoff | ❌ | ❌ | ❌ | ✅ **(unique)** — saving vs stopping, both in seconds |
| Setup-file reminder | ❌ | ❌ | ❌ | ✅ **(unique)** |
| Battle catch/defend forecast | ❌ | ❌ | ❌ | built **(unique)**, off by default — legibility |
| Safety direction (CPI vs your baseline) | ❌ | ❌ | ❌ | built, then removed |
| Rejoin indicator / slow-car-ahead | ❌ | ❌ | ✅ both | ❌ → needs research |
| Weather widget | ✅ monitor | ❌ | ✅ (+ wind) | ❌ → forecast strip, mid-term |
| Per-session profiles | auto layouts | ✅ | · | ❌ → mid-term |
| Input trace | ✅ | ✅ | ✅ | parked |
| Lap-time graphs | ✅ (3 blocks) | ❌ | · | parked |
| Gimmick blocks (heart rate, G-force, boost) | ✅ | ❌ | ❌ | non-goal |
| Column customization | ✅ | ✅ (core pitch) | ✅ resize/snap + driver tagging | non-goal — but resize itself is next up |
| Colour-blind mode | ❌ | ❌ | ❌ | ❌ → next up, first mover |
| VR | ✅ (headline) | ✅ (Oculus) | · | non-goal |
| Team fuel sharing | ✅ Pro | ✅ Pro | ❌ | non-goal — Garage 61 / iRacePlan own the endurance-team niche |
| Streaming tools | ✅ | ✅ | ✅ OBS | non-goal |
| Multi-sim | 7 sims | iRacing only | iRacing only | iRacing only, by design |
| Price | free + €4.90/mo Pro | free + €2.50–4.95/mo Pro | free, open source | free |
