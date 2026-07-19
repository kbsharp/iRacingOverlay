# Roadmap

What's planned, what's deliberately not, and the judgement behind both.
Informed by a July 2026 review of the two overlay suites this app is most
often compared against — [RaceLab](https://racelab.app) and
[iOverlay](https://ioverlay.app) — but this is **not a parity checklist**:
the point of the review was to decide what matters for *this* app, not to
copy the union of their feature lists.

For what's already implemented (every field and calculation), see
[FEATURES.md](FEATURES.md).

## The competitive read, in short

- **RaceLab** wins on breadth: 29 overlays and 80+ data blocks across 7 sims,
  VR-native rendering, a community layout library with auto-switching per
  car/series, Stream Deck integration, a post-race "Race Events" replay feed,
  team fuel sharing, driver tagging, and a tools manager. It's a platform.
- **iOverlay** is iRacing-only, like us, and sells deep per-column
  customization (21 driver columns, ~32 header telemetry values), plus a
  spotter/incident indicator, a pit entry/exit helper, a multiclass
  "faster traffic behind you" warning, track/flat maps, race control with
  camera switching, and streamer conveniences (Twitch chat, garage cover).
- **Our position** is neither: iRacing-only, opinionated, zero-config,
  driver-first. The overlay should be *right by default* — colour that means
  something, numbers a driver acts on, nothing to configure at midnight
  before a race. That position is worth defending, which is why several
  competitor features land under **non-goals** below rather than the backlog.

Worth knowing: some things here already have no equivalent on either site —
the setup-file reminder flash, the radar's corner-angled car placement
reconstructed from learned track shape (with no track database), and the
projected-iRating chip's honest race behaviour (race-only, sticky field on
disconnects, captured at the flag). The differentiator bets below build on
those same assets.

## Near-term — high value, fits the existing architecture

- **Projected Safety Rating chip** *(differentiator — neither competitor has
  it)*. The companion to the projected-iRating chip: live SR delta for this
  race from corners driven versus incident points (`WeekendInfo:
  TrackNumTurns` × laps vs `PlayerCarMyIncidentCount`). Both competitors show
  a raw incident count; neither answers "is this race helping or hurting my
  licence?". Same chip pattern, same Core-calculator shape, cheap to build.
- **Catch/defend pace trend on the relative** *(differentiator)*. Each
  relative row already shows a delta; track its per-lap trend and show
  "closing 0.3s/lap — on you in 6 laps" (or "caught before the flag: no").
  RaceLab has an "overtake alert" and "head to head"; a *forecast* — will
  this battle actually arrive, and when — is a different and more useful
  statement at racing speed.
- **Delta bar** (lap delta to session/all-time best) — parity; RaceLab has
  it, and it was already on the roadmap here.
- **Position-change arrows in the standings** (▲2 / ▼1 vs starting position)
  — parity with iOverlay's "positions gained" column; needs per-driver
  start-position tracking, already noted in FEATURES.md.
- **Pit status in the standings** — the relative flags pitting cars, the
  standings doesn't; iOverlay carries pit-stop status as a standings column.
  Extend the existing PIT badge across.
- Carried over from the previous roadmap: radar density pass, manufacturer
  badge on the relative, drag-to-resize widgets, a speed readout for the
  existing km/h / mph preference, configurable telemetry refresh rate,
  pinning the tray icon.

## Mid-term — bigger builds, still core to the mission

- **Track map widget** — the most conspicuous parity gap: both competitors
  ship one (iOverlay ships two). Ours has a twist neither can claim: `Core.
  Radar.TrackMap` already learns the circuit's real shape from the player's
  own driving, so the map needs **no track database** and is never missing a
  track or out of date after a resurface. Draw the learned line, place every
  car on it from `LapDistPct`, colour by class.
- **Input telemetry trace** (throttle/brake/steering, bars or scrolling
  graph) — both competitors have one; valuable for self-review and the
  single most-requested widget for streamers. Pure-Core ring buffer + a
  drawing pass.
- **Per-session-type profiles** — iOverlay lets training/qualifying/race each
  have their own configuration; RaceLab auto-switches whole layouts per
  car/series. Our existing "settings profiles per car/track" roadmap item
  should grow a session-type dimension: the practice layout (inputs, lap
  log) is genuinely not the race layout (relative, fuel, radar).
- **Weather forecast strip** — we show current temps/wetness; with iRacing's
  dynamic weather, the actionable question is "what's it doing in 20
  minutes?". A compact forecast readout on the session strip for
  wet-transition sessions.
- **Lap-time log/graph** — RaceLab has three lap-time blocks; a compact
  rolling lap history (with fuel per lap alongside) doubles as a stint
  review tool in endurance races.

## Differentiator bets — nobody has these

The features that would make this app *the reason* someone runs it alongside
(or instead of) the big suites. Each builds on an asset we already have.

- **Pit-exit position projection** (the flagship bet). Answer, live: *"if
  you pit this lap, you rejoin P8, 1.8s behind #12, clear of the GTD
  pack"*. The inputs exist — `CarIdxF2Time` field gaps, pit-lane time loss
  (learned from prior stops, or estimated from lane length + speed limit),
  fuel-fill time from the fuel calculator. Real teams have an engineer for
  this; solo racers guess. RaceLab's "Pitbox Helper" finds your stall and
  iOverlay's helper covers entry/exit lines — neither projects where you
  *come out*. This turns the fuel widget from a calculator into a strategist.
- **Multiclass traffic forecast**. iOverlay warns when faster traffic is
  already behind you; with our learned track geometry plus per-class pace we
  can forecast the *meeting point*: "GTP leader catches you in sector 2 next
  lap". For multiclass/endurance racing this is the difference between being
  a mobile chicane and cooperating with the pass — and it's only possible
  because we reconstruct track shape, so it composes with the track map
  widget above.
- **Push-vs-save fuel tradeoff**. The fuel widget already computes a
  "save to" burn target; pair it with the lap-time cost of driving to that
  number (learned from laps where burn was lower) and the catch/defend
  forecast: *"saving to 2.1 L/lap costs ~0.4s/lap and drops you to P7 by the
  flag — pitting costs P9"*. Strategy as one glanceable sentence.

## Non-goals — for now, and on purpose

- **Full column-level customization.** Both competitors sell it hard; it is
  their moat and our anti-feature. Every column here earns its place and the
  default *is* the product. Modest show/hide toggles (like the manufacturer
  badge) are fine; a layout editor is not.
- **Streaming toolkit** (Twitch chat, garage cover, scene tools, Stream
  Deck). A different product for a different user; the driver-facing overlay
  must not grow a broadcast wing. Revisit only if the driver feature set is
  truly done.
- **VR rendering.** RaceLab's headline feature, and a structural gap: WPF
  layered windows cannot render into an HMD — VR support means an OpenXR
  overlay layer or in-game injection, i.e. a second rendering stack. Not
  while the flat-screen feature set is still growing.
- **Team fuel sharing.** Both competitors gate it behind Pro for a reason:
  it needs accounts, a backend service, and invite management. Out of scope
  for a lightweight local overlay until there's a server-side story worth
  having.
- **Series/stats module, race-events replay, driver tagging, tools
  manager.** Platform features that serve the *between-races* user. This app
  serves the *in-the-car* user; breadth there is RaceLab's game and chasing
  it means losing ours.

## Market snapshot (July 2026)

| Capability | RaceLab | iOverlay | Us |
|---|---|---|---|
| Standings / relative / fuel | ✅ | ✅ | ✅ |
| Proximity radar | ✅ (radar + bars) | spotter indicator | ✅ corner-angled, learned geometry |
| Track map | ✅ | ✅ (2 forms) | ❌ → mid-term (no track DB needed) |
| Input trace | ✅ | ✅ | ❌ → mid-term |
| Delta bar | ✅ | via columns | ❌ → near-term |
| Projected iRating | gain shown | gain shown | ✅ full zero-sum model |
| Projected Safety Rating | ❌ | ❌ | → near-term **(unique)** |
| Battle catch/defend forecast | ❌ | ❌ | → near-term **(unique)** |
| Pit-exit position projection | ❌ | ❌ | → bet **(unique)** |
| Traffic meeting-point forecast | ❌ | behind-only warning | → bet **(unique)** |
| Setup-file reminder | ❌ | ❌ | ✅ **(unique)** |
| Per-session profiles | auto layouts | ✅ | ❌ → mid-term |
| Column customization | ✅ | ✅ (core pitch) | non-goal |
| VR | ✅ (headline) | ❌ | non-goal (structural) |
| Team fuel sharing | ✅ Pro | ✅ Pro | non-goal |
| Streaming tools | ✅ | ✅ | non-goal |
| Multi-sim | 7 sims | iRacing only | iRacing only, by design |
| Price | free + €4.90/mo Pro | free + €2.50–4.95/mo Pro | free |
