# Roadmap

What's planned, what's deliberately not, and the judgement behind both.
Informed by a July 2026 review of the two overlay suites this app is most
often compared against — [RaceLab](https://racelab.app) and
[iOverlay](https://ioverlay.app) — but this is **not a parity checklist**.
The review exists to sharpen one editorial test, applied to every candidate
feature:

> **Does it change a decision the driver makes in the car?**

If it doesn't, it's out — no matter how good it looks on a feature grid.
Heart-rate monitors, G-force meters and boost boxes are decoration; a number
that tells you *pit now, not next lap* is the product.

A readout that passes the first test then has to survive a second one, which
the removed safety chip is what taught us:

> **Is it in a unit the driver already has, and can they tell when it's wrong?**

This is *not* a rule against forecasts or estimates. "Closing 0.3s/lap, contact
in 1 lap" is a prediction and it is fine — seconds and laps are units every
driver already owns, and one lap later you can see whether it was right. Same
as "60% chance of rain": legibly uncertain, in units you understand, checkable
after the fact. What fails is a number you must be *taught* before it says
anything (`49 CPI`), or one measured against a baseline only this app can see,
so nobody can tell a right answer from a wrong one. Uncertainty is fine.
Illegibility isn't.

For what's already implemented (every field and calculation), see
[FEATURES.md](FEATURES.md).

## The focus

- **RaceLab** is a platform: 29 overlays and 80+ data blocks across 7 sims,
  VR, a community layout library, Stream Deck, streaming tools, a post-race
  replay feed. Breadth is its game.
- **iOverlay** is iRacing-only, like us, and sells customization: pick your
  own 21 standings columns and ~32 header telemetry values.
- **This app** sells judgement instead: iRacing-only, opinionated,
  zero-config, and *right by default*. The goal for now is a really strong
  core — the four widgets, finished properly, plus the strategy layer that
  no overlay currently provides — not a wide catalogue. The identity to
  build toward: **a race engineer, not a dashboard.**

Some of that identity already exists and has no equivalent on either site:
the setup-file reminder flash, the radar's corner-angled car placement
reconstructed from learned track shape (no track database), and the
projected-iRating chip's honest race behaviour (race-only, sticky field on
disconnects, captured at the flag). The bets below extend exactly these
assets.

## Core pass — finish the widgets we have

Near-term, high value, fits the existing architecture. Almost all of this
extends an existing widget rather than adding a new one.

- ~~**Standings: pit status**~~ — **done**. The relative's PIT badge now shows in
  the standings too, taking over the Int column while a car is in the lane — Int
  is meaningless for a car in its box, and Gap, which tells you where they
  rejoin, is preserved.
- **Relative: catch/defend pace trend** — **built, shipped, now off by default.**
  Every row carries a regressed per-lap gap rate (`▼ 0.4`) and, when the battle
  lands before the flag, the laps until it does (`3L`). Colour reports what it
  means for you — green a place you take, amber a place you lose — and a catch
  that misses the flag stays grey.

  The maths survives the first test: it changes a decision (chase or defend), in
  seconds and laps, checkable a lap later. It's the **presentation** that fails
  the second one. On the row it reads as a bare `▲ 0.2` with no unit and no
  referent — it never says *seconds per lap*, or per lap toward *what*. Sat
  between an iRating badge and a delta, both also small numbers, it's one more
  figure to decode rather than one to read. That's the CPI failure in a milder
  form: not a made-up unit this time, but still a number you must be taught
  before it says anything.

  Switched off behind `OverlaySettings.ShowPaceTrend` rather than deleted,
  because unlike the safety chip this has a **fixable** path — the quantity is
  right, the typography isn't — which is exactly what the manufacturer badge's
  toggle-as-staging-area is for. The column collapses to zero width when off, so
  the name reclaims the space. **What a legible version needs:** carry its unit
  or referent on the row (`-0.4s/L`, or a form that reads as *closing*), or drop
  the rate entirely and show only the part that's already a sentence — the
  laps-to-contact countdown, which needs no teaching. Worth resolving against the
  delta column too: two signed numbers side by side is the density problem, not
  just the label.
- ~~**Session strip: projected Safety Rating chip**~~ — **built, then removed.**
  It shipped as a corners-per-incident chip (`▲ 49 CPI`) rather than an SR
  delta, because iRacing has never published the CPI-to-SR conversion and only
  the *direction* is documented. That reasoning still holds — and it was the
  problem. CPI is not a unit drivers think in, the arrow was measured against a
  baseline only this app knew, and the honest version of the feature was a
  number that had to be explained before it meant anything. By the roadmap's own
  test, a figure you have to learn before it changes a decision isn't changing
  the decision. Removed rather than kept as a curiosity; the incident count it
  sat beside is the part that was always doing the work. **If it returns**, it
  needs a unit the driver already has — not a new one to teach.

  It was deleted rather than parked behind an off-by-default toggle on purpose.
  The manufacturer badge is off by default for a *fixable* reason (the mark set
  is incomplete), so its toggle is a staging area; this has no such path, so the
  toggle would have been permanent — the hedge this roadmap's own "the default
  *is* the product" rule exists to avoid. Nothing is lost: the whole
  implementation (`CpiHistory`, `SafetyTracker`, `SafetyChipViewModel`, the two
  chip styles, both strips' markup, and 28 tests) is intact at commit `7abef1b`
  and comes back with a cherry-pick if a better unit ever turns up. That is what
  git is for, and it costs nothing to maintain in the meantime.
- ~~**Delta bar**~~ — **done**, though narrower than written. It was scoped as
  "lap delta to session/all-time best"; the sim has no cross-session personal
  best and the app keeps no lap store, so an all-time reference would have had
  to be approximated and isn't offered. Session-fastest was dropped for a
  different reason: it needs a control to choose between references, and one
  number is the product. So it ships as **one reference — your own best this
  session** — using iRacing's own `LapDeltaToBestLap` rather than a second
  opinion reconstructed from lap distance. What the app adds is the part the sim
  doesn't: it **holds the finished lap's number for five seconds at the line**,
  where the sim resets it to zero exactly as it becomes worth reading, and it
  shows nothing in the pits, on an in-lap, or before there's a lap to compare
  against. See [FEATURES.md](FEATURES.md#delta--deltawindow--deltaviewmodel--coredeltadeltacalculator).

  **Now ships off by default** (opt-in from the tray), the one widget that does.
  It restates a number iRacing already shows in its own black box, so it earns a
  standing panel only for drivers who want it always up — the same test that
  parked the speed readout. The other four, which the sim doesn't already put in
  one place, stay on.
- ~~**Radar density pass**~~ — **done**, and the audit found a defect rather than a
  spacing question. `PixelsPerMeter` was a constant while the range it depicts is a
  setting (15–200 m), so the canvas and the range disagreed at every value except
  the one the constant was chosen for: at 200 m cars were placed 320 px out on a
  240 px canvas — off the widget entirely — and at 15 m the whole field huddled
  into the middle fifth while the rest stayed blank.

  So the scale is now derived from the range (`RadarLayout.ScaleFor`): a car at the
  limit of range sits exactly at the canvas edge at every setting. That fixes the
  density question as a side effect, because "how many pixels is a car's width" was
  never a spacing choice — it was this bug. The canvas width is derived too, from
  the fact that lateral offset can't exceed the arc walked and the calculator
  already bounds that by the range, so no car can be clipped off the side at any
  setting (a hairpin used to throw the inside car out over the glows).

  The one taste call: the default range drops 60 m → 40 m. With the scale following
  the range, the default range *is* the density control, and 60 m was showing cars
  the mirrors have long since handed over to the relative. 40 m is nine car lengths
  and five times the overlap zone the glow grades. The slider is still there for
  anyone who disagrees.

  **Needs a human eye in the sim**: whether 40 m feels right at racing speed, and
  whether a car queued right on your gearbox — which now overlaps the player mark
  by about half a blip rather than three-quarters — reads clearly enough.
- ~~**Radar: show when the geometry can't resolve a car**~~ — **done**. This was
  the one place in the app that failed the second test above: every other readout
  is either measured or legibly a forecast, while the radar drew *inferred*
  positions that looked exactly as authoritative as measured ones.

  Writing it clarified the problem, which was worse than the entry claimed. It
  isn't that a *straight* defeats the geometry; it's that lateral offset is
  derived from **along-track** offset bent through the learned shape, so a car
  level with you has all but the same `LapDistPct` and lands on the centreline
  in a corner too. No curvature test was needed in the end — the honest signal
  is the output, not the cause: a blip drawn closer than a car's width while
  inside the overlap box is in a position no two cars can occupy, which is the
  geometry saying *I don't know*.

  So that case now defers to iRacing's own `CarLeftRight`, the way the first-lap
  fallback does. The spotter names a *side*, not a *car*, so it only moves a blip
  when exactly one car is stacked and one side is reported; otherwise the blips
  stay put, drop to 45% opacity, and the glow still fires on every side reported —
  an unknown side is not an absent car. `Clear` is treated as an answer, so a
  nose-to-tail queue is left alone. Cars outside the overlap box keep their
  geometric placement, so the corner-angled read of the field ahead is untouched.
  See [FEATURES.md](FEATURES.md#radar--radarwindow--radarviewmodel--coreradar).
- ~~**Configurable telemetry refresh rate**~~ — **done**. A tray **Refresh Rate**
  submenu (60/30/20/15/10 Hz, 30 the default) sets how often the sim's feed is
  polled. The offered rates are exactly the integer divisors of iRacing's 60 Hz
  broadcast, so the number shown is the number delivered — a free-form slider would
  have to round to one of these anyway and then lie about it. The default and its
  reasoning are unchanged (30 Hz is the radar's floor for motion that slides rather
  than steps); the control just exposes the knob for anyone who wants 60 for
  smoothness or less for CPU. Applied live — IRSDKSharper reads `UpdateInterval`
  every frame, so it takes effect next frame with no reconnect — and the demo
  source retimes to match. The Hz→divisor logic lives in `Core.Telemetry.
  TelemetryRefresh` (tested); see [FEATURES.md](FEATURES.md#infrastructure-adapters).
- Carried-over polish: drag-to-resize widgets, pinning the tray icon.

## Needs research first

Not parked and not rejected — these pass the decision test, but there's an open
question that has to be answered *before* the build is worth starting, because
the answer decides whether there's a feature at all.

- **Manufacturer badge on the relative.** The badge already ships on the
  standings, off by default, and moving it to the relative looks like a
  half-hour of markup. It isn't, for two reasons that both need settling first.

  **The mark set is incomplete, and the relative punishes that harder.**
  `ManufacturerResolver` brand-matches substrings of `CarPath`/`CarScreenName`
  and deliberately isn't exhaustive — an unrecognised car resolves to `Unknown`
  and the column simply omits it. On the standings, a scattering of blanks down
  a long list reads as a list with some gaps. On the relative — six or seven
  rows, the cars actually around you — a blank next to the car you're fighting
  is the one place the badge had a job to do. The research: across the classes
  people actually race, what fraction of a typical relative's rows resolve? If
  it isn't near-total, the badge is worse than nothing there.

  **And it may be the wrong signal for that widget.** The relative already
  colours by `CarClassColor`, and in a single-make field every badge is
  identical — a column of the same mark, costing width on the app's densest
  row. The question is whether the badge changes a decision *on the relative*
  (recognising who's alongside you in multiclass) or is just the standings
  feature relocated. Answer that with real multiclass sessions, not reasoning.

  Until both are answered this stays out of the core pass. It remains the
  worked example of a toggle used as a staging area, referenced above and in
  the non-goals — that role doesn't depend on it reaching the relative.

## The strategy layer — the bets nobody has

The reason someone would run this app alongside (or instead of) the big
suites. All three answer the same kind of question — the one a solo racer
has no engineer for — and they share plumbing: field gaps, pace trends, and
the learned track shape.

Two of the three have landed; one is what's left. Building them moved the
common ground on a bit: `Core.Strategy` now exists, and with it the habit these
three share — when the sim won't tell you a number, measure it off the field
rather than modelling it from a constant nobody can check.

- ~~**Pit-exit position projection**~~ — **done**, and it turned out to hinge on
  one input rather than three. It ships as a strip on the fuel widget:

  ```
  IF YOU PIT NOW   P12 ▼6                       costs 29s
  19.4s behind #63 · 33.9s clear of #33
  ```

  The "estimated from lane length + speed limit" half of the plan was dropped:
  iRacing publishes the speed limit but **not** the lane length, and the loss also
  depends on where entry and exit rejoin the racing line. So the cost is measured
  instead — and measured off the **whole field**, not just the player, because a
  figure that only arrives after your own first stop arrives after the decision it
  was meant to inform. The measurement is a car's growth in `F2Time` across its
  pit-road visit, which is already "how far back did that drop them" in the
  currency the projection spends, with racing pace netted off by the cars that
  stayed out.

  Fuel-fill time was dropped too, for a roadmap reason rather than a technical
  one. Splitting the loss into transit plus your own service time needs a fill
  rate the sim never publishes, so the service half would be a constant nobody
  could check — the second test's failure mode exactly. Reporting the observed
  total keeps every part measured, and the strip **states the loss it used**
  ("costs 29s") so the projection is falsifiable rather than oracular.

  Working in `F2Time` throughout is what makes lapped traffic behave: a car a lap
  down already carries a lap time in its `F2Time`, so it sorts correctly without
  laps being counted separately — the same time-based reasoning that keeps the
  standings' laps-down from flickering.

  **The honest limitation**, stated on the widget's own terms in
  [FEATURES.md](FEATURES.md): it projects against the field as it stands *now*,
  and can't know who else is about to stop. Every pit-exit tool has this; showing
  the gaps rather than only a position is what lets the driver correct for it.

  **Needs a human eye in the sim**: whether the strip earns its space for the
  whole race or only near a stop — it's live from the third observed stop onward,
  which is always-true information but not always a live decision.
- **Push-vs-save fuel tradeoff**. The fuel widget already computes a
  "save to" burn target; pair it with the lap-time cost of driving to that
  number (learned from laps where burn was lower) and the catch/defend
  forecast: *"saving to 2.1 L/lap costs ~0.4s/lap and drops you to P7 by
  the flag — pitting costs P9"*. Strategy as one glanceable sentence.
- ~~**Multiclass traffic forecast**~~ — **done**. iOverlay warns when faster
  traffic is already behind you; this forecasts the *meeting point*. A strip on
  the relative names the nearest faster class closing from behind and where it
  arrives — `GTP #7 reaches you next lap · sector 3` — in that class's own colour.

  It ships built exactly on the plumbing this section promised: the gap is the
  relative's own on-track `EstTime` delta (now shared as
  `RelativeCalculator.TrackGapSeconds`), and the closing rate is the sim's
  per-class estimated-lap-time difference, so a same-class car (the catch/defend
  trend's job) or a slower one never appears. The **meeting point** is where the
  player will be after that many laps, named by the timing sector it lands in —
  which needed the one new input the sim already had and the app wasn't reading:
  `SplitTimeInfo`'s sector boundaries, now carried on `SessionMetadata`. Every
  part is measured or published; the sector is a unit off the driver's own
  timing screen, so nothing here has to be taught before it reads.

  It self-hides where it has no job: **collapsed** in single-class racing, and
  absent until the nearest faster car is within three laps — a warning you can't
  act on is noise. It needs no toggle for the same reason the pit-exit strip
  doesn't: it is either a live decision or it isn't on screen.

  **The honest limitation**, stated on the widget's own terms in
  [FEATURES.md](FEATURES.md): pace is per-*class*, not per-car, and it assumes
  today's pace holds — a car stuck in its own battle isn't really closing at
  class pace. So it is advisory, and like every other readout here it is right
  by default and visibly wrong when it's wrong, rather than oracular.

  **Needs a human eye in the sim**: whether the three-lap horizon and the
  "this lap / next lap / in N laps" granularity feel right at racing speed, with
  the real gaps a live multiclass field produces rather than the demo's tight
  pack.

## Awareness — mid-term

Bigger builds that still pass the decision test.

- **Track map widget** — the most conspicuous parity gap: both competitors
  ship one (iOverlay ships two), and it feeds real in-race decisions (where
  the yellow is, where your rival is, where traffic is building). Ours has
  a twist neither can claim: `Core.Radar.TrackMap` already learns the
  circuit's real shape from the player's own driving, so the map needs
  **no track database** and is never missing a track or out of date after a
  resurface. Draw the learned line, place every car on it from
  `LapDistPct`, colour by class. Also the natural canvas for the traffic
  forecast above.
- **Weather forecast strip** — we show current temps/wetness; with dynamic
  weather the actionable question is "what's it doing in 20 minutes?",
  which is a tyre/pit decision. A compact forecast readout on the session
  strip for wet-transition sessions.
- **Per-session-type profiles** — the practice layout is genuinely not the
  race layout. Grow the existing "settings profiles per car/track" roadmap
  item with a session-type dimension (iOverlay has per-session configs;
  RaceLab auto-switches whole layouts).

## Parked — good, but not core

Real features (both competitors ship them) that fail the in-car decision
test — they serve self-review and streaming, not the driver mid-race.
Revisit only once the core above is strong:

- **Input telemetry trace** (throttle/brake/steering).
- **Lap-time log/graph** (stint review).
- **Speed readout** (km/h / mph for the existing unit preference). Demoted here
  from the core pass, for the same reason the delta bar is now opt-in: it
  restates a number the driver already owns — the car's own dashboard has a
  speedometer a glance away — so a dedicated panel changes no decision the sim
  isn't already answering. It's cheap to add if a specific use turns up (a
  pit-lane limiter cue, say, which *would* be a decision), but as a bare speed
  block it stays parked.

## Non-goals — on purpose

- **VR.** Off the table. (It would also mean a second rendering stack —
  WPF layered windows can't reach an HMD — but the reason is focus, not
  difficulty.)
- **Gimmick data blocks** — heart rate, G-force meter, boost box,
  overtake-alert flashes. Decoration; fails the decision test outright.
- **Full column-level customization.** Both competitors sell it hard; it is
  their moat and our anti-feature. Every column here earns its place and
  the default *is* the product. Modest show/hide toggles (like the
  manufacturer badge) are fine; a layout editor is not.
- **Streaming toolkit** (Twitch chat, garage cover, scene tools, Stream
  Deck). A different product for a different user; the driver-facing
  overlay must not grow a broadcast wing.
- **Team fuel sharing.** Needs accounts, a backend service, and invite
  management. Out of scope for a lightweight local overlay.
- **Series/stats module, race-events replay, driver tagging, tools
  manager.** Platform features that serve the *between-races* user. This
  app serves the *in-the-car* user; breadth there is RaceLab's game and
  chasing it means losing ours.

## Market snapshot (July 2026)

| Capability | RaceLab | iOverlay | Us |
|---|---|---|---|
| Standings / relative / fuel | ✅ | ✅ | ✅ |
| Positions gained vs the grid | ✅ | ✅ (column) | ✅ per class |
| Proximity radar | ✅ (radar + bars) | spotter indicator | ✅ corner-angled, learned geometry |
| Track map | ✅ | ✅ (2 forms) | ❌ → mid-term (no track DB needed) |
| Delta bar | ✅ | via columns | ✅ vs your session best, held at the line |
| Projected iRating | gain shown | gain shown | ✅ full zero-sum model |
| Safety direction (CPI vs your baseline) | ❌ | ❌ | built, then removed — see above |
| Battle catch/defend forecast | ❌ | ❌ | built **(unique)**, off by default — legibility, see above |
| Pit-exit position projection | ❌ | ❌ | ✅ **(unique)** — learned lane cost, stated on the strip |
| Traffic meeting-point forecast | ❌ | behind-only warning | ✅ **(unique)** — faster class + sector, on the relative |
| Setup-file reminder | ❌ | ❌ | ✅ **(unique)** |
| Per-session profiles | auto layouts | ✅ | ❌ → mid-term |
| Input trace | ✅ | ✅ | parked |
| Lap-time graphs | ✅ (3 blocks) | ❌ | parked |
| Gimmick blocks (heart rate, G-force, boost) | ✅ | ❌ | non-goal |
| Column customization | ✅ | ✅ (core pitch) | non-goal |
| VR | ✅ (headline) | ❌ | non-goal |
| Team fuel sharing | ✅ Pro | ✅ Pro | non-goal |
| Streaming tools | ✅ | ✅ | non-goal |
| Multi-sim | 7 sims | iRacing only | iRacing only, by design |
| Price | free + €4.90/mo Pro | free + €2.50–4.95/mo Pro | free |
