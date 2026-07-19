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
- **Relative: catch/defend pace trend** *(unique — neither competitor has
  it)*. Each row already shows a delta; track its per-lap trend and show
  "closing 0.3s/lap — on you in 6 laps" (or "caught before the flag: no").
  A *forecast* of whether the battle actually arrives is a decision input —
  push now, or bank fuel — not another readout.
- **Session strip: projected Safety Rating chip** *(unique)*. The companion
  to the projected-iRating chip: live SR delta for this race from corners
  driven versus incident points (`WeekendInfo:TrackNumTurns` × laps vs
  `PlayerCarMyIncidentCount`). Both competitors show a raw incident count;
  neither answers "is this race helping or hurting my licence?" — which is
  precisely the drive-carefully-or-push decision. Same chip pattern, same
  Core-calculator shape, cheap to build.
- **Delta bar** (lap delta to session/all-time best) — the one *new* widget
  in this bucket, and it earns it: self-pacing against your own best is a
  core in-car decision tool, present in every serious overlay, and was
  already on the roadmap here.
- **Radar density pass** — the last widget not audited against the shared
  spacing rhythm; the question is the 150×240 field size and blip scale.
- Carried-over polish: manufacturer badge on the relative, drag-to-resize
  widgets, a speed readout for the existing km/h / mph preference,
  configurable telemetry refresh rate, pinning the tray icon.

## The strategy layer — the bets nobody has

The reason someone would run this app alongside (or instead of) the big
suites. All three answer the same kind of question — the one a solo racer
has no engineer for — and they share plumbing: field gaps, pace trends, and
the learned track shape.

- **Pit-exit position projection** (the flagship bet). Answer, live: *"if
  you pit this lap, you rejoin P8, 1.8s behind #12, clear of the GTD
  pack"*. The inputs exist — `CarIdxF2Time` field gaps, pit-lane time loss
  (learned from prior stops, or estimated from lane length + speed limit),
  fuel-fill time from the fuel calculator. RaceLab's "Pitbox Helper" finds
  your stall and iOverlay's helper covers entry/exit lines — neither
  projects where you *come out*. This turns the fuel widget from a
  calculator into a strategist.
- **Push-vs-save fuel tradeoff**. The fuel widget already computes a
  "save to" burn target; pair it with the lap-time cost of driving to that
  number (learned from laps where burn was lower) and the catch/defend
  forecast: *"saving to 2.1 L/lap costs ~0.4s/lap and drops you to P7 by
  the flag — pitting costs P9"*. Strategy as one glanceable sentence.
- **Multiclass traffic forecast**. iOverlay warns when faster traffic is
  already behind you; with our learned track geometry plus per-class pace
  we can forecast the *meeting point*: "GTP leader catches you in sector 2
  next lap". For multiclass/endurance racing this is the difference between
  being a mobile chicane and cooperating with the pass.

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
| Delta bar | ✅ | via columns | ❌ → core pass |
| Projected iRating | gain shown | gain shown | ✅ full zero-sum model |
| Projected Safety Rating | ❌ | ❌ | → core pass **(unique)** |
| Battle catch/defend forecast | ❌ | ❌ | → core pass **(unique)** |
| Pit-exit position projection | ❌ | ❌ | → bet **(unique)** |
| Traffic meeting-point forecast | ❌ | behind-only warning | → bet **(unique)** |
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
