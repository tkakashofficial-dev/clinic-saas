# Clinic SaaS — Design System

> Source of truth for all UI. Derived from the approved "Dentiva" reference
> (see `clinic-ui-mockup.jpg` / `dental-management-system.jpg` in this folder).
> Angular components must consume the tokens below — never hardcode colors.

## 1. The psychology (WHY these choices)

Dental software has a unique emotional context: **dental anxiety affects roughly
1 in 3 patients**, and staff use the product under time pressure. Every visual
decision below serves one of three goals: *calm, trust, speed*.

| Choice | Psychological effect |
|---|---|
| **Teal / mint green primary** | Health + healing + calm. Less aggressive than pure green, less cold/corporate than hospital blue. Signals "clean" without "clinical". |
| **Deep ink-green for dark surfaces** | Premium and grounded — the "expensive" feeling comes from dark, desaturated green, not from more bright color. |
| **Generous whitespace** | Subconscious association with sterility and order — what you want from a medical provider. Cramped UI reads as "dirty clinic". |
| **Rounded corners (12–16px) + soft shadows** | Approachability; softens the fear factor of a medical setting. |
| **Red is reserved exclusively for errors/destructive actions** | In a dental context red evokes blood. It must never appear decoratively. |
| **One bright accent, everything else quiet** | Premium = restraint. The teal draws the eye to the ONE next action per screen. |

### Per-persona UX principles

- **Receptionist screens** → speed: search-first patient lookup, large click
  targets, forms completable by keyboard alone, sub-2-click common actions.
- **Doctor screens** → context density: patient header (name, age, conditions)
  pinned while scrolling; today's schedule always one click away.
- **Admin screens** → calm authority: soft cards, few numbers, no alarm colors
  unless something is genuinely wrong.

## 2. Color tokens

```css
:root {
  /* Brand — teal/mint (from Dentiva reference) */
  --color-primary-50:  #E9FBF5;
  --color-primary-100: #C9F4E6;
  --color-primary-200: #93E9CE;
  --color-primary-300: #57D9B1;
  --color-primary-400: #24C99A;
  --color-primary-500: #00BD8F;  /* primary actions, brand moments */
  --color-primary-600: #00A37C;  /* button hover, focus rings */
  --color-primary-700: #008465;  /* links, text on light backgrounds (AA) */
  --color-primary-800: #086350;  /* deep accents */
  --color-primary-900: #0B4A3D;

  /* Ink — deep green-black (dark sections, sidebar, footer) */
  --color-ink-900: #0C2B23;      /* darkest surface */
  --color-ink-800: #123A30;
  --color-ink-700: #1B4A3E;

  /* Neutrals — slightly green-tinted, never pure gray */
  --color-bg:          #F4FAF7;  /* app background (mint-tinted white) */
  --color-surface:     #FFFFFF;  /* cards, panels */
  --color-border:      #E2EDE8;
  --color-text:        #10201B;  /* primary text */
  --color-text-muted:  #5B6F68;  /* secondary text */
  --color-text-invert: #F4FAF7;  /* text on ink/primary surfaces */

  /* Semantic — clinical clarity, used ONLY for meaning */
  --color-success: #16A34A;
  --color-warning: #D97706;
  --color-error:   #DC2626;      /* errors & destructive ONLY (blood association) */
  --color-info:    #2563EB;

  /* Appointment status (consistent everywhere: chips, calendar, lists) */
  --status-scheduled: var(--color-info);
  --status-completed: var(--color-success);
  --status-cancelled: #94A3B8;   /* muted slate — cancelled is neutral, not alarming */
}
```

### Accessibility rules (WCAG 2.1 AA — non-negotiable)

- Body text: `--color-text` on `--color-bg`/`--color-surface` (≥ 12:1 ✅)
- **Never** use `primary-500` for text on white (fails AA) — use `primary-700`.
- White text is allowed on `primary-600` and darker, and on all ink shades.
- Focus states: 2px ring `primary-600`, visible on every interactive element.
- Status is never conveyed by color alone — always icon + label with the color.

## 3. Typography

| Role | Font | Weight | Notes |
|---|---|---|---|
| Headings | **Plus Jakarta Sans** | 600–700 | Geometric, warm, premium — matches reference |
| Body / UI | **Inter** | 400–500 | Screen-optimized, excellent numerals for tables |
| Data/tabular | Inter with `font-variant-numeric: tabular-nums` | 400 | Aligned columns in lists & reports |

Scale (rem): `12 / 14 (base UI) / 16 (body) / 18 / 22 / 28 / 36`.
Line-height 1.5 body, 1.2 headings. Both fonts are free (Google Fonts).

## 4. Spacing, radius, elevation

- Spacing scale: **4px base** — `4 / 8 / 12 / 16 / 24 / 32 / 48 / 64`.
- Radius: inputs & buttons `10px`, cards `16px`, pills/chips `999px`.
- Elevation: prefer **borders + subtle shadow** over heavy shadows:
  `0 1px 2px rgb(12 43 35 / 0.06), 0 4px 12px rgb(12 43 35 / 0.06)`.
- Dark surfaces (`ink-900`): sidebar navigation and marketing footer only —
  the working app stays light (long-session eye comfort in bright clinics).

## 5. Component rules

- **Buttons**: primary = `primary-500` bg, ink text `#06362B` or white per
  contrast; secondary = white bg + border; destructive = `error` (rare).
  One primary button per view.
- **Forms**: labels above fields (faster scanning than floating labels),
  validation message under field in `error` with icon, never placeholder-only.
- **Tables/lists**: row height ≥ 48px, hover highlight `primary-50`,
  sticky header, pagination bottom-right.
- **Status chips**: soft background (status color at ~12% opacity) + status
  color text + dot icon.
- **Empty states**: friendly illustration/icon + one-line explanation + the
  primary action ("No patients yet — Register your first patient").
- **Loading**: skeletons (not spinners) for lists and cards.

## 6. Voice & microcopy

- Calm, plain language. "Couldn't save the patient — the phone number is
  already registered" not "Error 409: Conflict".
- Buttons say what they do: "Register patient", "Book appointment" — never "Submit" or "OK".
