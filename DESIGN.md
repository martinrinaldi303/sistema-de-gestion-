---
name: Electric Barbershop & Salon OS
colors:
  surface: '#131315'
  surface-dim: '#131315'
  surface-bright: '#39393b'
  surface-container-lowest: '#0e0e10'
  surface-container-low: '#1b1b1d'
  surface-container: '#201f21'
  surface-container-high: '#2a2a2c'
  surface-container-highest: '#353437'
  on-surface: '#e5e1e4'
  on-surface-variant: '#bccbb9'
  inverse-surface: '#e5e1e4'
  inverse-on-surface: '#303032'
  outline: '#869585'
  outline-variant: '#3d4a3d'
  surface-tint: '#4ae176'
  primary: '#4be277'
  on-primary: '#003915'
  primary-container: '#22c55e'
  on-primary-container: '#004b1e'
  inverse-primary: '#006e2f'
  secondary: '#ddb7ff'
  on-secondary: '#490080'
  secondary-container: '#6f00be'
  on-secondary-container: '#d6a9ff'
  tertiary: '#ffb4ae'
  on-tertiary: '#68000a'
  tertiary-container: '#ff8a83'
  on-tertiary-container: '#860011'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#6bff8f'
  primary-fixed-dim: '#4ae176'
  on-primary-fixed: '#002109'
  on-primary-fixed-variant: '#005321'
  secondary-fixed: '#f0dbff'
  secondary-fixed-dim: '#ddb7ff'
  on-secondary-fixed: '#2c0051'
  on-secondary-fixed-variant: '#6900b3'
  tertiary-fixed: '#ffdad7'
  tertiary-fixed-dim: '#ffb3ad'
  on-tertiary-fixed: '#410004'
  on-tertiary-fixed-variant: '#930013'
  background: '#131315'
  on-background: '#e5e1e4'
  surface-variant: '#353437'
  canvas-base: '#0A0A0A'
  surface-card: '#18181B'
  surface-elevated: '#1F1F23'
  border-subtle: '#27272A'
  border-prominent: '#3F3F46'
  neon-green-glow: rgba(34, 197, 94, 0.25)
  neon-purple-glow: rgba(168, 85, 247, 0.25)
  neon-purple-light: '#C084FC'
  crimson-muted: '#F87171'
typography:
  display-lg:
    fontFamily: Space Grotesk
    fontSize: 40px
    fontWeight: '700'
    lineHeight: 48px
    letterSpacing: -0.03em
  display-md:
    fontFamily: Space Grotesk
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Space Grotesk
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Space Grotesk
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  headline-sm:
    fontFamily: Outfit
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Outfit
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Outfit
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Outfit
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  label-lg:
    fontFamily: Space Grotesk
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0.02em
  label-md:
    fontFamily: Space Grotesk
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-sm:
    fontFamily: Space Grotesk
    fontSize: 10px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.06em
  metric-tabular:
    fontFamily: Space Grotesk
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 24px
    letterSpacing: -0.01em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-dense: 0.5rem
  margin: 1.5rem
  margin-compact: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
  space-2xl: 3rem
---

## Brand & Style

This design system defines an assertive, high-voltage desktop application built specifically for salon management, appointment scheduling, and cashier operations. Rooted in modern rocker-retro aesthetics, the UI balances the raw energy of an urban neon barbershop with the razor-sharp precision demanded by daily commercial management.

### Personality & Emotional Response
The interface must feel commanding, nocturnal, and tactile. It avoids the bland, sterile conventions of enterprise tools in favor of deep atmospheric blacks illuminated by electric emeralds and radioactive violets. Users should feel an immediate sense of mastery, style, and streamlined focus:
- **Atmospheric & Nocturnal:** Ultra-deep base tones eliminate screen fatigue during long studio shifts under low-ambient salon lighting.
- **Electric Precision:** High-contrast neon accents draw direct attention to live revenue figures, appointment states, and urgent chair allocations.
- **Tactile Modernism:** Crisp boundaries, subtle neon back-glows, and fluid pill-shaped chips elevate POS tables beyond monotonous spreadsheet layouts.

### Design Movement: Dark Neon-Modernism
A fusion of dark minimalism, neon cyber-lounge, and high-contrast typography. Surfaces are layered through meticulous dark value shifts rather than drop shadows, punctuated with glowing borders and selective neon accents that bring rock 'n' roll attitude into an institutional workflow.

## Colors

The palette is tuned specifically for an ultra-dark environment, ensuring high visual ergonomics, instant hierarchy detection, and chromatic punch.

### Palette Roles
- **Primary (`#22C55E`):** Neon Emerald. Reserved for key transactional verbs, active sidebar tabs, booking confirmations, positive cashier balances, and high-frequency operational triggers.
- **Secondary (`#A855F7` / `#C084FC`):** Electric Violet. Anchors branding badges, promotional salon packages, loyalty markers, and VIP client tags.
- **Tertiary (`#EF4444` / `#F87171`):** Muted Crimson. Strict operational signifier for destructive states, cancellation of services ("Anular"), voided sales receipts, and chair conflicts.
- **Neutral Stack (`#0A0A0A` to `#3F3F46`):** Deep volcanic grays forming canvas levels, elevated cards, segmented control wells, and ultra-crisp hairline dividers.

### Usage Rules
- Avoid pure white (`#FFFFFF`) for large body blocks; reserve it for primary numerical metrics and prominent display headings. Use lower contrast grays (`#A1A1AA`, `#71717A`) for secondary meta-information.
- Never use full-saturation backgrounds on large containers. Neon hues belong exclusively to interactive boundaries, text highlights, indicators, and radial accent glows.

## Typography

The typographic hierarchy pairs the brutalist, architectural energy of **Space Grotesk** with the clean geometric legibility of **Outfit**.

### Rules & Application
- **Display & Section Headers:** Set in Space Grotesk with snug negative letter spacing (`-0.02em` to `-0.03em`) for a strong, poster-like presence on dashboard summary panels and register receipts.
- **Interface & Operational Reading:** Set in Outfit. Its broad apertures and geometric balance maintain flawless readability on appointment grids, customer histories, and drop-down selectors.
- **Financial & Tabular Data:** All ARS ($) transactions, checkout tallies, and inventory counters must enforce `font-feature-settings: "tnum" 1` alongside Space Grotesk to guarantee column alignment across high-speed POS checkouts.
- **Uppercase Meta-labels:** Micro-labels (such as payment methods, chair numbers, and ticket timestamps) leverage `label-sm` with widened tracking (`0.06em`) for clear scanning at a glance.

## Layout & Spacing

The layout is built for dedicated desktop workstation viewports (1280px minimum width, scaled comfortably up to 4K displays). It follows a fixed-fluid hybrid structure designed for continuous operation without erratic reflows.

### Grid & Density
- **Global Structure:** A fixed persistent 260px left-hand control rail paired with a fluid multi-panel workspace (split between agenda timelines, customer dossiers, and quick-access register drawers).
- **Inner Column Mechanics:** A 12-column layout utilizing `1rem` gutters for analytical screens, collapsing to `0.5rem` dense gutters for the daily booking schedule view.
- **Rhythm Principle:** Component internals use increments of 4px. Card padding strictly adheres to `space-md` (16px) or `space-lg` (24px) to retain breathing room despite high information density.

## Elevation & Depth

Depth in this design system is created through tonal layering and razor-thin hairline borders rather than traditional diffused drop shadows, preserving clarity on dark backgrounds.

### Elevation Tiers
1. **Canvas (Base - `#0A0A0A`):** The structural floor holding panels and background rail systems. Zero elevation.
2. **Surface Container (`#18181B`):** Standard resting state for cards, tables, and modal shells. Delimited by a solid hairline stroke of `1px solid #27272A`.
3. **Elevated Surface (`#1F1F23`):** Active popovers, floating dropdown menus, and hovered card panels. Outlined with `1px solid #3F3F46`.
4. **Neon Atmospheric Radiance:** Selected elements, active tabs, and primary action buttons generate a soft, contained ambient bloom:
   - Green Glow: `0 0 20px rgba(34, 197, 94, 0.22), 0 0 4px rgba(34, 197, 94, 0.45)`
   - Purple Glow: `0 0 20px rgba(168, 85, 247, 0.22), 0 0 4px rgba(168, 85, 247, 0.45)`
   - Never stack neon glows across more than two adjacent items to prevent visual fatigue.

## Shapes

The design system adopts a balanced structural geometry where medium-curved containers house hyper-smooth pill elements.

### Curvature Taxonomy
- **Standard Containers (`rounded-lg` / 16px):** Operational cards, modal sheets, schedule columns, and transaction receipt boxes.
- **Interactive Controls (`rounded-md` / 8px):** Form inputs, action buttons, table row hover wraps, and tooltips.
- **Pill Geometry (`rounded-full` / 9999px):** Status badges (e.g., "En Espera", "Atendiendo", "Cobrado"), stylist chair badges, filter tags, and quick-increment counter chips.

## Components

### Buttons
- **Primary Action (Brand Green):** Solid `#22C55E` fill with `#0A0A0A` text, weighted in `Space Grotesk` SemiBold. On hover: slight brightness scale with green neon glow (`0 0 16px rgba(34, 197, 94, 0.35)`).
- **Secondary / VIP (Brand Purple):** Semi-transparent `#A855F7` background (15% opacity), solid `#C084FC` text, and a `1px solid rgba(168, 85, 247, 0.4)` border.
- **Destructive ("Anular" / Cancel):** Transparent fill with a `1px solid #EF4444` border and `#F87171` text. On hover: `#EF4444` fill with solid white text.
- **Ghost / Utility:** Transparent fill, `#71717A` text, transitioning to `#18181B` surface on hover.

### Badges & Chips
- Ultra-compact pill format (`rounded-full`, 4px vertical padding, 12px horizontal padding).
- Must utilize dimmed backgrounds with saturated text:
  - *Active / Ready:* `rgba(34, 197, 94, 0.12)` background, `#22C55E` text.
  - *Appointment Booked / In Process:* `rgba(168, 85, 247, 0.12)` background, `#C084FC` text.
  - *Canceled / Conflict:* `rgba(239, 68, 68, 0.12)` background, `#F87171` text.

### Data Tables (Sales & Appointments)
- Abandon heavy grid lines. Utilize clean horizontal rows separated by a `1px solid #1F1F23` divider.
- Alternate row backgrounds: `transparent` resting, transitioning to `#18181B` with smooth cursor feedback.
- Column headers: Uppercase `Space Grotesk` 10px tracking `0.06em` in `#71717A`.
- Monetary amounts ($ ARS) right-aligned, set in `metric-tabular` for effortless auditing.

### Input Fields
- Background set to `#121214`, outlined with `1px solid #27272A`.
- Height: 40px standard for fast data-entry workflows.
- Active focus state: Hairline stroke shifts to `#22C55E` accompanied by a micro inner shadow, preventing the default blue system outline.

### Cards & Panels
- Base card: `#18181B` with a `1px solid #27272A` outline and 16px border-radius.
- Highlight card (e.g., active daily register total): Top border accented with a 2px neon emerald stroke or a subtle radial gradient wash across the upper edge.

### Specialty Barbershop / Salon Modules
- **Timeline Chair Slot:** High-contrast time markers with an integrated pill badge representing the assigned barber and service type.
- **Quick-Checkout Bar:** Fixed bottom bar displaying running transaction amounts with immediate single-click shortcuts for Cash, Debit, and Mercado Pago.