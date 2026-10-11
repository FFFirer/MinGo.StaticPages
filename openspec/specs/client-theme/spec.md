# client-theme

## Purpose

Browser-local light/dark/auto theme preference for the management console, applied before first paint and switched entirely on the client (no Blazor interop).

## Requirements

### Requirement: Local theme preference persistence
The system SHALL persist the user's theme mode in the browser as `localStorage` key `mingo-theme`, whose value MUST be one of `light`, `dark`, or `auto`. Missing or invalid values MUST resolve as `auto`.

#### Scenario: Save selected mode
- **WHEN** the user selects a theme mode in the switcher
- **THEN** `localStorage` key `mingo-theme` holds that mode string

#### Scenario: Invalid stored value falls back to auto
- **WHEN** `mingo-theme` is missing or not one of `light` / `dark` / `auto`
- **THEN** the system resolves the theme as `auto` and does not throw

### Requirement: First-paint theme application
The system SHALL apply the resolved theme to `document.documentElement` as `data-bs-theme` via a synchronous inline script in `App.razor` `<head>` before first paint, using only local browser state.

#### Scenario: Restored dark theme without flash
- **WHEN** the stored mode resolves to dark and the document loads
- **THEN** `html[data-bs-theme="dark"]` is set before first paint, and the page does not briefly render light styles

#### Scenario: Works before Blazor starts
- **WHEN** the page loads under `prerender: false` or on a static/login shell
- **THEN** the theme is applied without waiting for an interactive Blazor circuit or WebAssembly

### Requirement: Theme mode resolution
The system SHALL map mode `light` and `dark` to that effective theme, and mode `auto` to `dark` when `prefers-color-scheme: dark` matches, otherwise `light`. The effective theme MUST be written to `html[data-bs-theme]` as `light` or `dark` only.

#### Scenario: Auto follows system dark
- **WHEN** mode is `auto` and the OS/browser prefers dark
- **THEN** `html[data-bs-theme]` is `dark`

#### Scenario: Auto follows system light
- **WHEN** mode is `auto` and the system does not prefer dark
- **THEN** `html[data-bs-theme]` is `light`

### Requirement: Pure-client theme switcher
The system SHALL provide a theme switcher UI with entries for `light`, `dark`, and `auto`, rendered as static HTML marked with `data-theme-toggle` / `data-theme-value`, and driven only by an ES module (`wwwroot/js/theme.js`). The switcher MUST NOT use Blazor JS interop, `IJSRuntime`, or server endpoints.

#### Scenario: Switch theme from navigation
- **WHEN** the user picks a mode in the switcher under the main layout
- **THEN** the effective theme updates immediately, `mingo-theme` is updated, and the selected entry is visually indicated

#### Scenario: Switch theme on login shell
- **WHEN** the user picks a mode in the switcher on `EmptyLayout` (login)
- **THEN** the same persistence and immediate theme update occur

#### Scenario: No Blazor dependency
- **WHEN** Blazor has not started or the circuit is disconnected
- **THEN** the switcher still updates theme and localStorage

### Requirement: ESM theme module
The system SHALL load `wwwroot/js/theme.js` as an ES module and use it to bind switcher controls, persist mode changes, and keep UI selected state in sync. Theme logic MUST NOT require a Blazor component lifecycle.

#### Scenario: Module binds static markup
- **WHEN** `theme.js` executes after document parse
- **THEN** all `[data-theme-toggle]` elements are bound and their selected state matches the current mode

### Requirement: Cross-tab and system theme sync
The system SHALL update the effective theme when another tab changes `mingo-theme`, and SHALL re-resolve `auto` when `prefers-color-scheme` changes.

#### Scenario: Another tab changes theme
- **WHEN** a different browsing context sets `mingo-theme` to another valid mode
- **THEN** this tab applies the new mode without a full reload

#### Scenario: System preference changes under auto
- **WHEN** mode is `auto` and `prefers-color-scheme` changes
- **THEN** `html[data-bs-theme]` updates to match the new system preference
