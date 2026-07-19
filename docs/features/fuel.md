# Fuel widget

`FuelWindow` / `FuelViewModel` / `FuelCalculator` + `FuelStrategyCalculator` +
`LapTimeTracker`

A strategy calculator, not just a burn-rate readout — the numbers shown are the
ones a driver acts on mid-race.

## Layout

252px wide, same borderless/transparent/topmost/draggable behaviour as the
relative. Default position on first launch (`Left=80, Top=140`), then restored
from saved settings.

Spacing runs on the **same rhythm as the list widgets**: a 10px panel inset and
6px between blocks, with the headline readout at 26px and the labelled figures
at 15px (the shared `Value` style). Keep it there — a looser inset with every
figure at one size made a widget carrying eight numbers as tall as a twelve-car
standings table.

## Displayed fields

- Current fuel level (`nn.nn L`) and laps of running left in the tank at current
  burn (`FuelEstimate.EstimatedLapsRemaining`).
- Used/lap and last lap, both from `FuelCalculator`'s rolling average.
- Race laps remaining (whole laps, from
  `FuelStrategyCalculator.EstimateRaceLapsRemaining`).
- **To finish**: total fuel needed to reach the end of the race
  (`avgLitersPerLap × raceLapsRemaining`).
- **Margin badge**: laps of fuel spare (green) or short (red) at the finish,
  labelled "LAPS SPARE"/"LAPS SHORT". Hidden entirely (`HasStrategy = false`)
  until both a burn average and a race length are known. Styled as the same
  tint-plus-edge chip as the license/iRating badges, and **sharp-cornered**
  (`CornerRadius="3"`) — rounded pills belong to the pre-flat theme, don't
  reintroduce one.
- **Add**: litres to add at the next stop to finish with a half-lap safety
  buffer (0 when already enough).
- **Save to**: the burn rate per lap that would still make it to the finish on
  current fuel without stopping — a save target when the driver is short.

## `FuelCalculator` (per-lap burn)

Detects lap changes from raw `(lap, fuelLevel)` frames.

- Rolling window, default last 5 laps (`windowSize`).
- A mid-lap fuel *increase* over 0.2 L (`RefuelThresholdLiters`) is treated as a
  pit stop and invalidates that lap's measurement. Small fuel-reading noise
  under that threshold is not.
- A lap-counter jump of more than +1 (missed a lap's telemetry) is not recorded
  — the interval isn't a clean single lap.
- A lap-counter *decrease* (tow back to pits, session restart) re-baselines the
  current-lap tracking but **keeps prior recorded laps** in the average — those
  are still representative of this car/track/fuel load.

## `LapTimeTracker` (rolling lap time)

Same detection pattern as `FuelCalculator`: rolling window default 5, ignores
multi-lap jumps, re-baselines on lap-counter decrease. Feeds
`FuelStrategyCalculator.EstimateRaceLapsRemaining` for timed races.

## `FuelStrategyCalculator`

- `Compute(currentFuel, avgLitersPerLap, raceLapsRemaining, safetyMarginLaps =
  0.5)` returns `FuelStrategy.Unknown` until both burn average and race length
  are known — a null in either input short-circuits the whole result, rather
  than proceeding on "some data".
- `EstimateRaceLapsRemaining` prefers the sim's own lap count for lap-limited
  races (`SessionLapsRemainEx`, sentinel `32767` = unlimited/timed). For timed
  races it derives laps from time remaining ÷ average lap time, **rounded up** —
  the lap in progress must still be completed. Returns null for an
  unlimited/unknown-length session with no usable lap time yet.
- iRacing's "no time limit" sentinel (~604800s / one week) is treated as
  unlimited, matching `SessionFormat.TimeRemaining`.

## Known limitations

- No fuel-per-stop split for multi-stop strategies — "Add" assumes one more stop
  covers the rest of the race.
- Safety margin defaults to 0.5 laps; configurable 0–5 in Settings.
- Demo mode always simulates a short (~4 minute) timed race so the margin reads
  comfortably positive. The red "short" state is real code but isn't exercised
  by the demo without the dev panel's **Set critical** control.

---

# Setup reminder strip

`SetupReminderTracker` (Core), rendered by the fuel widget.

A reminder, not a data readout: it exists to catch the "raced on the low-fuel
qualifying setup" mistake — forgetting to load the race setup before Qualifying
or Race starts.

**It lives on the fuel panel, not in its own window.** It's two lines of text
that say the same kind of thing the fuel widget does ("what is my car running"),
read in the same place (the pits, not at speed) — a dedicated window spent a
whole set of chrome on it. `WidgetIds` does not list a `"SetupWindow"`; a
`settings.json` written when one existed still carries its keys, but every
per-widget map is read by lookup, so stale entries are never consulted.

## Displayed fields

One line, below the fuel figures:

- A session-type chip (e.g. "RACE", "QUALIFY", "PRACTICE") — amber when the
  session is a Qualify or Race, neutral grey otherwise, so even without the
  flash a glance tells you whether the setup matters right now.
- The currently loaded setup file name (iRacing's `DriverSetupName`, `.sto`
  extension stripped by `SetupFormat.DisplayName`).
- A "MOD" tag when `DriverSetupIsModified` is set — the loaded setup has been
  changed since it was last loaded or saved.

## The flash

The whole *fuel* panel pulses a soft amber wash (`#00FFB03D` ↔ `#70FFB03D`, 0.7s
each way, looping) for the first 60 seconds of any session whose type contains
"Race" or "Qualif" (case-insensitive — covers "Race", "Heat Race", "Qualify",
"Open Qualify", "Lone Qualify"). It does **not** flash for Practice or Warmup.

`SetupReminderTracker.Update` is fed every telemetry frame and detects the
session transition by watching `TelemetrySnapshot.SessionNum` change. The flash
window is measured relative to `SessionTimeSeconds` at that transition, so it's
correct whether `SessionTime` is session-relative or cumulative. Launching the
overlay *during* an already-running Qualify/Race session still flashes
immediately — there's no "already seen it" state carried across app restarts.

**Implementation rule:** the flash animates a `ColorAnimation` targeting the
implicit style-owner element via a property path
(`(Border.Background).(SolidColorBrush.Color)`) rather than
`Storyboard.TargetName`. WPF doesn't allow a `Style`'s triggers to target a
sibling-named brush by name — only the element the style is attached to, or
elements inside a `ControlTemplate`.

## Switching it off

`OverlaySettings.ShowSetupReminder` (Settings → Tuning, "Show the setup reminder
on the fuel widget"). Defaults to **on**, including for a settings file written
before the property existed. Off hides the strip *and* suppresses the flash —
`FuelViewModel.ShouldFlash` is gated on the setting as well as the tracker, so
switching it off mid-pulse stops the animation rather than waiting for the
window to expire.

## Known limitations

- The flash window defaults to 60 seconds; configurable 5–300 in Settings.
- No acknowledge/dismiss action — the flash times out. Deliberate: the intent is
  a passive visual reminder, not an interactive one.
- Demo mode starts already in "Race" so the flash is visible from app startup.
  Use the dev panel's **Cycle session** button to see Practice → Qualify → Race
  transitions and re-trigger it on demand.
