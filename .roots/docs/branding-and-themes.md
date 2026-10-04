# Branding & Themes

JeffyGPT ships Look 07 Take: a centered den chat shell with rust accents, hard borders, and a soil (dark) / parchment (light) pair.

## Product identity

| Item | Value |
|------|-------|
| Product name | **JeffyGPT** (capital J, capital GPT) |
| Visual concept | Take den — soil/parchment, rust nest, IBM Plex Sans wordmark, IBM Plex Mono labels |
| Chrome copy | Wordmark `Jeffy GPT`, decorative tally `2 tonight`, idle status from `idleHuntStatus.ts` |
| Roles | User **Upstairs**, assistant **Jeffy**, send **Hunt** |
| Tool UI | In-transcript **Hunt** box while tools run; **Hunted** label remains (hover/focus lists called tools) |

Agent-facing rules: `.cursor/rules/branding.mdc`

## Logo asset

**Official icon (do not modify):**

```
src/JeffyGPT/JeffyGPT.Web/src/assets/brand/jeffygpt-icon.svg
```

The mark is Jeffy under the floor: parchment boards, a soil gap, two gleam eyes looking up, and a rust baseboard, inside a bone frame. Colors are the den palette (`#efe6d4`, `#0c0a08`, `#f0c878`, `#c75c3e`, `#1c1610`). The tile is a sharp square. App chrome still shows the wordmark without this icon.

**Usage:**

- Import via `JeffyGptLogo` — never inline duplicate SVG
- Chrome uses `showWordmark` and `showIcon={false}`
- Favicon: `public/favicon.svg` (copy of official asset)

```tsx
import { JeffyGptLogo } from './components/brand/JeffyGptLogo';

<JeffyGptLogo showWordmark showIcon={false} />
```

### Wordmark styling

- **Jeffy** — `--jgpt-wordmark-jeffy` (bone)
- **GPT** — `--jgpt-wordmark-gpt` (nest/rust)

## CSS design tokens

Defined in `src/JeffyGPT/JeffyGPT.Web/src/index.css`. Themes switch via `data-theme="light"` or `data-theme="dark"` on `<html>`.

Canonical table is `src/JeffyGPT/JeffyGPT.Web/src/index.css`. The user guide in `.userdocs/user-guide.html` describes how to switch theme. Do not add a separate stylesheet for that guide.

### Shared den accents

| Token | Value | Use |
|-------|-------|-----|
| `--nest` / `--rust` | `#c75c3e` | Hunt button, GPT wordmark, user bubble tint, status dot |
| `--nest-deep` | `#9c3c28` | Hover / user bubble border |

### Light theme (parchment den)

| Token | Value | Use |
|-------|-------|-----|
| `--soil` | `#efe6d4` | Page background |
| `--tunnel` | `#f7f1e4` | Den surface |
| `--joist` | `#e4d8c4` | Chrome bar |
| `--bone` | `#1c1610` | Body text |
| `--bone-dim` | `#5c5348` | Muted text |
| `--whisker` | `#5c5348` | Meta labels, dashed hunt box |
| `--gleam` | `#8a6a20` | Assistant labels, status, cursor |
| `--den-edge` | `#c4b49a` | Borders |
| `--message-fill` | `#f3ead8` | Assistant bubble fill |
| `--composer-fill` | `#eadfcb` | Composer bar |
| `--code-bg` | `#e8dcc6` | Code surfaces |
| `--error` | `#9c3c28` | Errors |
| `--focus-ring` | `var(--nest)` | Focus outline |

`--surface` maps to `--soil`, `--surface-elevated` to `--tunnel`, `--text-primary` to `--bone`.

### Dark theme (soil den)

| Token | Value | Use |
|-------|-------|-----|
| `--soil` | `#0c0a08` | Page background |
| `--tunnel` | `#17130f` | Den surface |
| `--joist` | `#2a2219` | Chrome bar |
| `--bone` | `#e6dccb` | Body text |
| `--bone-dim` | `#a89f8f` | Muted text |
| `--whisker` | `#6e675e` | Meta labels, dashed hunt box |
| `--gleam` | `#f0c878` | Assistant labels, status, cursor |
| `--den-edge` | `#2e261c` | Borders |
| `--message-fill` | `#14110d` | Assistant bubble fill |
| `--composer-fill` | `#100d0a` | Composer bar |
| `--code-bg` | `#1a1612` | Code surfaces |
| `--error` | `#e07058` | Errors |
| `--focus-ring` | `var(--nest)` | Focus outline |

### Usage in CSS

```css
/* Good — uses tokens */
.card {
  background: var(--surface-elevated);
  color: var(--text-primary);
  border: 1px solid var(--den-edge);
}

/* Bad — hard-coded colors break theme switching */
.card {
  background: #fff;
  color: #111827;
}
```

## Theme switching

### ThemeProvider

React context in `theme/ThemeProvider.tsx`:

| Export | Description |
|--------|-------------|
| `theme` | Current theme: `'light'` \| `'dark'` |
| `setTheme(theme)` | Set explicit theme |
| `toggleTheme()` | Switch between light and dark |

### Persistence

- **localStorage key:** `jeffygpt-theme`
- **Fallback:** `prefers-color-scheme` media query when no stored preference

### Flash prevention

`index.html` includes an inline script that applies the theme to `<html>` before React loads, preventing a flash of the wrong theme.

### ThemeToggle

Dashed den-chrome button (moon/sun) in `DenChrome` calls `toggleTheme()`.

## UI chrome requirements

Per branding rules, new pages should:

1. Use `AppShell` for the soil page and centered den frame
2. Use `DenChrome` + `JeffyGptLogo` for branding (wordmark without icon)
3. Use theme CSS tokens for all colors
4. Verify both parchment and soil themes

## HTML metadata

From `index.html`:

| Meta | Value |
|------|-------|
| `<title>` | JeffyGPT |
| `theme-color` | `#c75c3e` |
| `application-name` | JeffyGPT |
| `description` | JeffyGPT — self-hosted LLM tool-calling application |
| Fonts | IBM Plex Sans, IBM Plex Mono (Google Fonts) |

## Hunt / idle chrome

Idle status in the top-right of `DenChrome` is picked by `pickIdleHuntStatus()` on page load and whenever a request finishes (`sending` true → false):

`hunting`, `stalking prey`, `lurking`, `crawling`, `setting traps`, `checking traps`, `fighting cat`, `sleeping`

The decorative tally chip reads **2 tonight**. It is flavor, not a live count.

Tool activity is not a composer popup. See [Chat Flow](./chat-flow.md) and [Frontend](./frontend.md) for Hunt / Hunted transcript behavior.

## Exploratory look prototypes

Standalone HTML chat-shell mockups (not wired into the React app) live under `prototypes/jeffygpt-looks/`. The shipped web UI matches look 07 Take. Open `index.html` in a browser.

| File | What changed from 07 |
|------|----------------------|
| `07-mouser.html` | Original. Manifesto, app icon, nest-pink, hunt tally |
| `07-lean.html` | No manifesto, no icon. Same soil, joists, trail, pink tuft |
| `07-moss.html` | Lean chrome. Nest pink becomes moss batt |
| `07-brother.html` | Lean chrome. Italic Jeffy lines. Send, not Hunt |
| `07-rods.html` | Lean chrome. Scotopic soil. Status is hatch shut |
| `07-take.html` | Lean chrome. Pink becomes rust. Wordmark is Jeffy GPT. No joists, trail, or nest tuft. **Shipped look.** |

Shared den CSS is `mouser.css`. The six pages share a sticky switcher (`review-nav.css`, `review-nav.js`). Serve from the **repository root** (`python -m http.server`) so relative paths resolve.
