# Radar widget

`RadarWindow` / `RadarViewModel` + `Core.Radar`

A top-down proximity radar, LMU-style: the player car sits dead centre facing
up, with the nearby field drawn as car icons at their real positions relative to
you — **angled to match the track**, so a car alongside through a corner leans
the way the corner does. The whole widget **hides itself when nobody is near**
and reappears the instant a car comes into range.

## Layout

A fixed 150×240 field (`RadarLayout`). Unlike every other widget the radar is
**chrome-free** — no panel, no border, no background, just marks floating over
the track — because it lives in the driver's forward view where a box would
occlude more than it explains. Its one piece of furniture is a dashed line level
with the player's axles, to judge overlap against. Still
borderless/topmost/draggable. **No header label** — a radar is self-evident.
Fixed position on first launch (`Left=600, Top=470`, right column).

## How it works — reconstructing positions iRacing won't give you

iRacing's telemetry exposes each car's `LapDistPct` (how far round the lap it
is) but *no* world position or heading for other cars — only the **player's**
heading (`Yaw`). So the radar learns the track's shape from the player's own
driving and reuses it to place everyone else. Pure, tested logic lives in
`Core.Radar`:

- **`TrackMap`** records the player's heading into 720 buckets keyed by
  `LapDistPct`, filling every bucket driven through between samples (so one
  clean lap maps the whole track, not several). A large forward jump — teleport,
  tow, reset — fills only the current bucket, so it can't smear a false line. It
  reports `IsReady` once ≥55% of the lap is mapped (about one lap).
- **`RadarGeometry`** walks that heading table from the player's `LapDistPct` to
  another car's, integrating the track's curve, to recover the car's position
  (`RightMeters`/`ForwardMeters`) **and** orientation (`RelativeAngleRad`) in the
  player's local frame. On a straight the walk is a straight line (cars sit
  directly ahead/behind, parallel); through a corner it bends, so the car ends
  up off to the side and rotated.
- **`RadarCalculator`** builds the `RadarBlip` list for every rostered car
  within range (`DefaultRangeMeters` = 60 m along the track, 15–200 in
  Settings), excluding pit and pace/spectator cars, then grades each side's
  danger.
- **`TrackLengthParser`** turns iRacing's `WeekendInfo:TrackLength` (`"3.70 km"`,
  occasionally miles) into the metres the geometry needs.

`RadarViewModel` owns the `TrackMap`, feeds it the player's heading each frame
(only while on track and moving >3 m/s — a stationary heading would poison a
bucket), runs the calculator, and maps metres to canvas pixels in
`RadarBlipViewModel` (fixed-size icons, positions scaled at `PixelsPerMeter`).
Blip slots update in place; the collection only resizes when the number of
nearby cars changes.

## Visual behaviour

The widget is visible only when there's something to show: the positional radar
once the track is mapped and a car is in range, the spotter fallback during the
first lap, or a small "radar" placeholder before the sim reports (so the
auto-hiding widget can still be dragged into place).

**Traffic is white, you are green, danger is red** — three colours, one meaning
each. Opponents are deliberately *not* class-coloured here, though they are in
the standings and relative: at a blind-spot glance "is that me or them" beats
"what class is that", and a green-class opponent next to a green player mark was
genuinely ambiguous.

## Proximity glow

The thing you actually read at speed is a red glow off the door on the side a
car is on, fading with how much it matters rather than blinking on and off at a
threshold. `RadarDanger` (pure, tested) grades each side 0–1 from the blips:
intensity peaks with a car level and close alongside, and falls to zero past 9 m
lateral or 7 m longitudinal.

**Crucially it ignores cars within `MinLateralMeters` (1.2 m) of your own line.**
A train running nose-to-tail on the racing line is queued traffic, not a
side-by-side; without that floor both glows sit lit for whole laps, which is the
opposite of useful.

The glow ellipses live *outside* both the positional and fallback subtrees in
`RadarWindow.xaml`, bound directly to `LeftDanger`/`RightDanger` — so both modes
drive the same visual.

## First-lap fallback

Until `TrackMap.IsReady` there's no shape to place cars against, so the widget
falls back to iRacing's coarse `CarLeftRight` spotter signal, via `RadarFormat`'s
`HasCarLeft`/`HasCarRight` classifiers. It drives the same glow at full strength
with no blips — same visual language, less detail — rather than a separate set
of blocks. Once the lap is mapped, the positional radar takes over.

## Known limitations

- **Needs ~one lap to learn the track**, per session/track. Before then it's the
  coarse left/right fallback.
- **Lateral offset on a dead straight isn't resolvable.** With no per-car
  lateral telemetry, two cars perfectly side-by-side on a straight both map onto
  the centreline; the geometry separates cars by the track's curvature, which is
  zero on a straight. The angle it *can* show (parallel) is still correct. The
  spotter fallback's left/right remains the honest read for that exact case.
- **Left/right handedness assumes iRacing's `Yaw` is anticlockwise-positive**
  (standard, and what the geometry expects). Worth a live confirmation against
  the sim that a car actually on your left shows on your left — if mirrored,
  it's a one-line sign flip in `RadarBlipViewModel`/`RadarGeometry`.
- No audio cue — visual only.

## Reviewing it offscreen

`scripts\render.ps1 radar radar-danger` renders both states. The demo field runs
nose-to-tail at zero lateral offset, which `RadarDanger` correctly reads as
queued traffic, so **the graded positional glow cannot be produced from demo
data** — `radar-danger` renders the spotter fallback's full-strength glow
instead. Checking the graded fade needs the sim. See
[DEVELOPMENT.md](../DEVELOPMENT.md#debugging).

Demo mode synthesises a weaving circuit (`SimulatedTelemetrySource.DemoHeading`,
3000 m) and seats the field in a pack around the player, so the radar populates
with visibly angled cars within a lap.
