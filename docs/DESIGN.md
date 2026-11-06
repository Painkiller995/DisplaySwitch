# Design system

DisplaySwitch should feel like a native Windows 11 utility with its own identity: calm surfaces, one confident accent, and a large rendering of *your* monitor at the centre of the experience. Everything visual comes from three files:

| File | Contents |
|---|---|
| `src/DisplaySwitch.App/Design/Tokens.axaml` | Colour, type, spacing, radius and elevation tokens for the Light, Dark and HighContrast themes |
| `src/DisplaySwitch.App/Design/Icons.axaml` | The line-icon set |
| `src/DisplaySwitch.App/Design/Styles.axaml` | Component styles, chosen by class (`primary`, `inputCard`, `caption`…) |

Views never set colours or font sizes inline; they choose a role.

## Principles

1. **The monitor is the hero.** The illustration is drawn at the monitor's real aspect ratio (a 32:9 ultrawide looks like one) and shows what is on screen now.
2. **One action per card.** Clicking a card switches. Customisation sits behind a quiet edit button.
3. **Say only what is known.** Success styling is reserved for confirmed switches; uncertainty is explained, not hidden.
4. **Quiet by default.** Confirmations dismiss themselves after 5 s; anything needing attention stays until dismissed.

## Colour

Semantic tokens, with the contrast ratio measured against the surface they sit on:

| Token | Light | Dark | Use |
|---|---|---|---|
| `Brush.Accent` | `#5B4CF0` | `#A99CFF` | Primary buttons, live input, focus |
| `Brush.Text` | `#17171C` (17.4:1) | `#F3F3F6` (16.1:1) | Body text |
| `Brush.TextSecondary` | `#5E5E6E` (6.4:1) | `#ABABB8` (7.5:1) | Supporting text |
| `Brush.TextTertiary` | `#6E6E7C` (5.0:1) | `#8C8C99` (5.1:1) | Overlines, captions |
| `Brush.TextOnAccent` | `#FFFFFF` (5.6:1) | `#14112B` (7.8:1) | Text on accent fills |
| `Brush.Success` / `Warning` / `Danger` | `#16794A` / `#8A5A00` / `#BE2B43` | `#5BD69A` / `#F2C14E` / `#FF8597` | Status banners |

All text meets WCAG 2.2 AA (4.5:1). The **HighContrast** theme is applied automatically when the OS requests it: pure black and white, a yellow accent and focus ring, no shadows or translucency. Fluent controls take the accent from `FluentTheme.Palettes`.

The logo badge keeps its brand gradient (`#7B6CFF → #3D74F5`) in every theme, like a printed logo.

## Typography

Inter on every platform for a consistent identity. Contextual alternates are turned off so input codes read `0x11`, not `0×11`.

| Role | Size / weight |
|---|---|
| `display` | 28 / SemiBold, −0.4 tracking |
| `title` | 20 / SemiBold |
| `subtitle` | 15 / SemiBold |
| body | 14 / Regular |
| `caption` | 12 / Regular, secondary colour |
| `overline` | 11 / SemiBold, +0.8 tracking, upper case |

## Spacing, shape and elevation

- **Spacing:** a 4-point grid with steps 4, 8, 12, 16, 20, 24, 32. Page padding 32; card padding 16.
- **Radii** grow with component size so nested corners stay concentric: 6 (small), 8 (controls), 12 (tiles), 14 (cards), 18 (sheets), pill.
- **Elevation:** `Card` (resting), `CardHover`, `Overlay` (sheets and flyouts) and `Hero` (a coloured glow under the monitor).

## Components

| Component | Notes |
|---|---|
| Input card | 184 × 156. States: rest, hover (raised 2 px, stronger shadow), pressed (98 % scale), focus (2 px ring offset 3 px), **active** (accent border, tinted fill, filled icon tile, "Live" chip), switching (spinner + "Switching…"), disabled. |
| Monitor hero | Bezel, screen with a radial glow when an input is live, and a stand. Marked decorative for assistive technology; the same information is available as text. |
| Status banner | Info, success, warning and error, plus progress. A polite live region; offers "Try again" when retrying makes sense. |
| Keycaps | Show a shortcut as keys; decorative, since the card's spoken name already includes the shortcut. |
| Sheet (editor) | Centred over a scrim. Focus moves into it; Esc or the scrim closes it. |
| Quick switcher | A compact flyout anchored to the taskbar or menu-bar corner, with a brightness slider; closes when it loses focus. |
| Display sliders | One row per control: icon, label, slider, value. Color temperature uses a warm-to-cool gradient track. Arrow keys step by 1, Page Up/Down by 10. |

## Iconography

Original 24 × 24 line icons, all drawn with a 1.6 px stroke and round caps and joins. The `Icon` control maps the shared 24-unit grid to its size, so every glyph has the same weight at any size, and it inherits the text colour of its container.

## Motion

| Duration | Use |
|---|---|
| 120 ms | Hover colour |
| 200 ms | State changes (border, lift, icon tile) |
| 900 ms loop | Spinner (only while visible) |

Transitions are declared only under the `.animated` window class. The class is removed when the OS asks for reduced motion (Windows *Animation effects*) or the user turns on **Reduce motion**; everything then changes instantly, and spinners show a static arc.

## Accessibility checklist

- Every interactive element has an accessible name. Cards announce name, port, live state and shortcut, for example *"DisplayPort, currently on screen, shortcut Ctrl Alt 1"*. Verified with Windows UI Automation.
- Visible focus on every control. Tab order follows reading order. Number keys 1–9 switch inputs, F5 refreshes, Esc closes sheets and leaves Settings.
- Pointer targets meet WCAG 2.2 target size (2.5.8): the smallest, the banner's dismiss button, is 28 × 28 px. Standard buttons are at least 32 px tall.
- Status changes are announced politely without moving focus.
- The shortcut recorder never traps focus: Tab and Shift+Tab still move on.
