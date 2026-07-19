# Style — the visual language, and the current refinement pass

What the overlay looks like on purpose, what must not change, and the active
refinement plan. Written after a July 2026 side-by-side of our rendered
widgets against in-game RaceLab screenshots (the styling reference this app
is most often compared to). FEATURES.md records what *is*; this file records
the visual judgement and what's being refined toward.

## The identity (protected — these do not change)

The overlay reads as **lightweight and terminal-lite**: dark, near-opaque
top-lit panels the track shows through faintly, soft 6px corners, restrained
colour that always means something. That understatement *is* the product's
feel — the refinement pass below must sharpen it, not sand it off.

Protected, explicitly:

- **Dark panels, light text.** RaceLab's timing widgets are the opposite —
  white broadcast-TV rows with dark text. That's their identity; ours is the
  dark material. Never flip it.
- **The panel material**: near-opaque, top-lit gradient, 1px top highlight,
  soft 6px corners, drop-shadow lift. Uniform across widgets.
- **The warm amber player row** — wash + outer glow. RaceLab uses a flat
  khaki fill; ours reads as *lit* and is better. Keep.
- **Hue = meaning discipline** (CLAUDE.md): class colour, license tier,
  green/red = gain/loss, purple = session best, amber = you/caution. No
  decorative colour.
- **Segoe UI Variable** at the correct optical sizes, tabular numerals,
  weights one step heavy (greyscale AA). See FEATURES.md § Typography.
- **Chips as tint + 1px hue edge** — the terminal-flavoured chip language.
- **The radar stays chrome-free.**

And the borrowed motifs we deliberately do **not** take, because they are
RaceLab's signature, not ours: seven-segment/LCD readout fonts, the italic
"speed slash" accents, carbon-fibre textures, glossy pill gauges, white row
backgrounds.

## What actually makes RaceLab read as the gold standard

Studied from live screenshots, not the marketing renders. It is not the
white rows. Four habits transfer to our material; they are the whole pass:

1. **Ruthless glance hierarchy.** In their relative, the time delta — the
   one number read at 200kph — is the largest, heaviest thing in the row.
   Everything else steps down behind it. In ours, the delta renders at the
   same size as the car number.
2. **Solid, confident colour where colour is the message.** Their class
   strips are fully saturated blocks with dark text — DP gold, GTE cyan,
   GT3 pink read across the cockpit instantly. Our class banners are muted
   washes; at a squint the three classes are three similar dark tints.
3. **Instruments, not number sheets, for continuous quantities.** Their fuel
   widget has a tank gauge with E/full scale and a pit-window bar; ours
   presents eight numbers. A quantity with a natural zero and max deserves a
   bar the eye can read without parsing digits.
4. **Per-widget personality inside one family.** Their timing widgets, dash
   panel, and fuel calculator each look like what they are, while sharing
   DNA. Ours are deliberately uniform — right call at MVP, but the fuel
   widget being "a smaller standings" is why it reads flatter than theirs.

## The refinement pass — complete

All five moves shipped, one commit each, render-verified. What landed:

1. **Relative: the delta is the headline** — `16px` Bold against the driver
   name's `13px`, so the eye lands on the one number read at 200kph.
2. **Standings: solid class name-plates** — the class short name in a filled
   block of the sim's own class hue, its label darkened or lightened by
   measured luminance (`RatingFormat.PrefersDarkText`) since class colours are
   series-defined.
3. **Fuel: a tank gauge bar** — `6px`, fill against usable tank capacity, tick
   at fuel-to-finish, green when it clears and red when short.
4. **Session strip: a headline figure** — time/laps remaining split out of the
   joined `"RACE · 3:24"` string and stepped up; the label drops to secondary.
5. **Chip loudness** — one step was enough: the tinted-chip edge went
   `#8A<hue>` → `#66<hue>` and the row now reads delta → name → chips. Chip
   **hue and text were left alone deliberately** — they are iRacing's own
   licence colours, and dimming them would cost the tier its instant read,
   which the protected list exists to prevent. PIT and the projected-iRating
   chip keep the louder edge: they are event flags, not steady furniture.

**Still needs a human eye in the sim** (a static render can't settle these):
the fuel bar and the delta emphasis at racing speed, and chip contrast over
moving scenery rather than a flat backdrop.

Deliberately **not** taken in this pass: whole-widget redesigns, the radar (its
density question is a roadmap item), the settings window (already its own
kind of window), any new data fields (that's the roadmap's business).

## Workflow for the next pass

- One move per commit, `render.ps1` before and after, PNGs eyeballed
  against this file's intent.
- If a move fights the protected list, the protected list wins — bring the
  conflict back to this doc rather than compromising in XAML.
- When a move lands, update FEATURES.md in the same commit if it changes
  anything that file describes; prune the move from this list once shipped.
