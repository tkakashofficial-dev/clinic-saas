# Klivia — Designer Guide

*How to design screens that look like they were always part of the product.
Tokens live in `design-system.md`; this guide is how to USE them.*

---

## 1. The feeling we sell

**Calm, clean, premium.** Dental patients are anxious; staff are busy. Every
screen should lower the pulse, not raise it. Practical test: if a screen looks
"exciting", it's probably wrong — aim for *quietly expensive*.

## 2. Hard rules

1. **Tokens only.** Never a raw hex in a component — `var(--color-…)` always.
2. **One primary button per view.** The teal draws the eye to THE next action.
3. **Red = errors/destructive only.** Never decorative (blood association).
4. **The clinic is the brand in-app.** Klivia appears only as "Powered by".
5. **Every list needs its trio:** loading (skeletons, never spinners),
   empty state (icon + one line + the primary action), error (plain words +
   what to do next). A blank screen is a bug.
6. **Text links underline on hover; button-shaped things never do.**

## 3. Page anatomy (copy this structure)

```
.page-header      h1 + .sub  (left)         primary action (right)
.toolbar          search / filter-chips / date-nav
.card             table (.table) or content
  .paginator      "Page x of y · n total"        Previous / Next
```

Forms open in a **drawer** (slide-over from the right), never a new page:
`drawer-header` (title + ✕) / `drawer-body` (the form) / `drawer-footer`
(Cancel + primary). Backdrop click closes.

## 4. Picking the right control

| Situation | Control |
|---|---|
| 2–4 options (gender, role) | `app-segmented` — all options visible, one tap |
| 5+ options or options with sublabels (doctor) | `app-select` — custom dropdown |
| Birth dates (far past) | `app-date-field` — typed DD/MM/YYYY with masking |
| Near dates (booking) | native date input inside the day-navigator pattern |
| Multi-pick that combines (staff roles) | toggle `filter-chip`s |

Labels sit **above** fields. Placeholders show examples, never instructions.
Validation message goes under the field in `--color-error` with the message
saying how to fix it.

## 5. Status is a system

One status = one color = everywhere the same (chips, donut, calendar):
Scheduled blue · Waiting (CheckedIn) violet · In progress amber ·
Completed green · Cancelled slate. Status chips always pair the color with a
**label + dot** — color alone is not information (WCAG).

## 6. Charts

Pure SVG/CSS only (no chart libraries — weight budget). Bars for time series,
donut for composition (max ~5 slices, always with a legend), usage bars for
limits. Chart teal gradient `primary-400 → primary-600`; empty bars use
`--color-border`. Numbers use `tabular-nums`.

## 7. Microcopy voice

Calm, plain, specific. Buttons say what they do.

| ❌ | ✅ |
|---|---|
| Submit | Register patient |
| Error 409: Conflict | A patient with this phone number already exists. |
| Workspace | Clinic |
| Are you sure? | You'll be the Admin of the new clinic. Its data is completely separate. |
| Loading… | (skeleton rows) |

## 8. Accessibility checklist (per screen)

- Text ≥ AA contrast (never `primary-500` text on white — use `primary-700`)
- Focus visible on every interactive element (global focus ring exists — don't remove it)
- Hit targets ≥ 38px; table rows ≥ 48px
- Drawers: `role="dialog"` + `aria-label`; icon-only buttons: `aria-label`
- Works at 360px wide (forms collapse to one column automatically)

## 9. Designing a new screen — the checklist

1. Who uses it — receptionist (speed), doctor (context density), admin (calm overview)?
2. What is the ONE primary action? (that's your teal button)
3. Sketch with the page anatomy from §3 — don't invent new chrome
4. Define the trio: loading / empty / error
5. Mobile pass at 360px
6. Words per §7 — read every label aloud; if it sounds like software, rewrite it
