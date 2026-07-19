# Feature inventory

What the app currently does, so it can be checked against instead of
re-derived from memory. **If this drifts from the code, the code wins** — but
update the relevant page in the same commit as the change that invalidates it.

This is an index. The detail lives in [`docs/features/`](features/), one page
per area, so a change to one widget means reading and editing ~100 lines rather
than a thousand.

## Widgets

| Page | Covers |
|---|---|
| [Standings](features/standings.md) | Full class-grouped field table, SoF, manufacturer badges, gaps/interval |
| [Relative](features/relative.md) | Compact nearest-cars glance widget, session strip, colour coding |
| [Fuel](features/fuel.md) | Burn rate, strategy to the finish, and the setup reminder strip |
| [Radar](features/radar.md) | Top-down proximity radar, track-shape learning, proximity glow |

## Everything else

| Page | Covers |
|---|---|
| [Telemetry & adapters](features/telemetry.md) | `TelemetrySnapshot`, session metadata, the live SDK adapter and the demo source |
| [Settings & persistence](features/settings.md) | The settings window, tuning values, units, saved layout |
| [Dev tools](features/dev-tools.md) | Tray icon, app icon, auto-update, dev control panel |
| [UI shell](features/shell.md) | `App.xaml` palette, panel material, typography |
| [Formatting](features/formatting.md) | `Core.Formatting` helpers and `RatingFormat` |

For setup/build/test commands see [DEVELOPMENT.md](DEVELOPMENT.md); for the
short pitch and prerequisites see the [README](../README.md).

## Writing rule for these pages

Record **what the app does now, and the rules that must not be broken** — not
the history of how it got here. "The panel previously ran an 18/12 inset" is a
commit message; "the panel runs a 10px inset on the same rhythm as the list
widgets" is documentation. Where a past mistake is worth protecting against,
state it as a constraint ("badges are sharp 3px — rounded pills belong to the
pre-flat theme"), not as a story.

## Not yet implemented

Tracked in the [README roadmap](../README.md#roadmap): delta bar, extending the
manufacturer badge to the relative, drag-to-resize widgets, a speed readout for
the existing km/h / mph preference, a configurable telemetry refresh rate,
per-car/track settings profiles, and pinning the tray icon.
