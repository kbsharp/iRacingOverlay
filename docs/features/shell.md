# UI shell — `App.xaml`

Shared resources used by every window: the single source of truth for the visual
style.

**Colour is purposeful, not decorative.** Each hue means exactly one thing —
class, license tier, iRating tier, lap status, "this is you". `Accent` (azure) is
reserved for branding (window header labels) and the A-license badge
specifically. Reaching for the accent colour for everything is what made the
first pass at the relative widget read as flat.

## Panel material

- **`PanelBackground`** — a neutral graphite/charcoal vertical gradient
  (`#242A34` → `#1A1D23` → `#0E0F13`) at **~94% alpha** (`F0`): near-opaque, the
  track shows only faintly through. The gradient is deliberately wider than a
  flat fill — a lighter, slightly cooler top falling to a deep bottom reads as a
  surface **lit from above**, the material cue that stops the panel looking like
  a flat console rectangle. Keep it a low-saturation neutral so colour stays
  reserved for things that carry meaning.
- **Depth without being glassy**: panels use a `CornerRadius` of `6px`, plus
  `PanelSheen` (a faint specular highlight concentrated at the top third) and
  `PanelTopHighlight` (a 1px lit "catch light" along the top edge) to sell each
  panel as a raised object rather than a printed rectangle.
- **`PanelBorder`** — a **top-lit gradient** (bright `#78FFFFFF` along the top
  edge, dimming to a dark warm-grey down the sides), not a flat hairline: a
  subtle bevel that reads as a physical edge and keeps the widget a distinct
  object over busy track scenery.
- **`RowStripe`** — a very faint white (`#0DFFFFFF`) for zebra striping on every
  other row (standings and relative).
- **`Separator`**, **`RowHover`**, **`HeaderBand`** — structural chrome.

## Colour resources

- `Accent` (azure), `Positive` (green), `Negative` (red), `Warning` (amber) —
  status colours.
- `PlayerHighlight` / `PlayerBorder` — the "this is you" row wash and outline;
  warm amber, intentionally not `Accent`, so it doesn't compete with the
  license/iRating/class colours on every row.
- `LicenseRookie`/`LicenseD`/`LicenseC`/`LicenseB`/`LicenseA`/`LicensePro` —
  iRacing's real license-class colours (red/orange/yellow/green/blue/gold),
  driving the `LicenseBadgeBackground`/`LicenseBadgeText` styles via
  `DataTrigger`s on `RelativeRowViewModel.LicenseTier`.
- `IRatingLow`/`IRatingMid`/`IRatingHigh`/`IRatingElite` — a separate
  grey/teal/violet/magenta family for the iRating badge, so it's never confused
  with the license badge beside it.
- `TextPrimary`/`TextSecondary`/`TextMuted` — text hierarchy, three clearly
  separated steps. **`TextMuted` is deliberately kept well clear of the panel
  material**; a dimmer value makes captions read as grey noise rather than as a
  quiet tier of a hierarchy.
- `LapAheadText` (red-ish) / `LapBehindText` (blue-ish) — relative row colouring.
- `FastestLap` (purple) — the standings' session-fastest best lap.
- `Caption`, `Value` and `Timing` styles for the
  small-uppercase-label / large-number / tabular-figure patterns.
- `DevButton` — flat, rounded button style for the dev control panel.

**A row's class-colour bar is the one colour that isn't a static resource** — it
comes from live sim data, so it can't be a fixed set of `DataTrigger`s. The
shared `ClassColorBrush.Resolve` helper (`App/ViewModels/`) parses a row's
normalised hex string into a frozen `SolidColorBrush` (grey fallback on a parse
failure); both the relative and standings rows bind their class bar to it.

## Typography

Segoe UI Variable ships **three optical sizes and they are not
interchangeable** — each is drawn for a size band, so picking the wrong one is a
rendering bug, not a taste call:

| Resource      | Family                    | Use for                       |
| ------------- | ------------------------- | ----------------------------- |
| `FontSmall`   | Segoe UI Variable Small   | ≤11px — captions, badges, PIT |
| `FontText`    | Segoe UI Variable Text    | 12–28px — window default, rows |
| `FontDisplay` | Segoe UI Variable Display | ≥29px — the big fuel readout  |

Display is drawn for headlines — thin stems, tight tracking — so at row size it
renders spindly and washed out. That, not the palette, is what made the widgets
read as "terminal text". **All windows default to `FontText`**, with `FontSmall`
on captions/badges.

Two things compound this, and are why weights here run one step heavier than
they would in a normal window:

- **`AllowsTransparency="True"` disables ClearType.** WPF falls back to
  greyscale antialiasing on layered windows, which thins stems further. There is
  no way to get ClearType back without giving up the transparent overlay.
- Widget text is small and sits over moving scenery.

So: driver names, positions, gaps and captions are `Bold`, not `SemiBold`;
secondary figures are `SemiBold`, not `Normal`. **WPF maps both `SemiBold` and
`DemiBold` to weight 600 — there is no step between 600 and `Bold` 700.**

**Numeric fields set `Typography.NumeralAlignment="Tabular"`** (via the `Timing`
and `Value` styles, or inline). Without it, proportional figures make ticking
values jitter and right-aligned columns read ragged.

## Shared window behaviour

All windows share: `DropShadowEffect` for panel lift, a `6px` `CornerRadius`,
`BooleanToVisibilityConverter` (`BoolToVis`) for conditional badges, and the
drag-to-move + right-click-exit interaction pattern.

Default (first-run) positions are laid out non-overlapping: standings top-left,
relative bottom-left, fuel/radar in a right column, dev controls far right.
After that each widget's position is restored from saved settings (see
[settings](settings.md#layout-persistence)). UI scale is applied per-window via a
`ScaleTransform` on the content root.
