/**
 * Client-only theme module (ESM).
 * Storage contract (must match the classic inline script in App.razor):
 *   localStorage['mingo-theme'] = 'light' | 'dark' | 'auto', read order
 *   localStorage -> sessionStorage. Writes go to localStorage first and fall
 *   back to sessionStorage when localStorage is unavailable (private mode,
 *   blocked storage on insecure origins), so the choice still survives
 *   same-tab navigations. Missing/invalid values resolve as 'auto'.
 */
export const THEME_KEY = 'mingo-theme';
export const MODES = ['light', 'dark', 'auto'];

function store(kind) {
    try {
        return kind === 'session' ? window.sessionStorage : window.localStorage;
    } catch {
        // Accessing the storage object itself can throw (some private modes).
        return null;
    }
}

function readRaw() {
    for (const kind of ['local', 'session']) {
        const s = store(kind);
        if (!s) continue;
        try {
            const value = s.getItem(THEME_KEY);
            if (value !== null) return value;
        } catch {
            // Unreadable store — keep looking at the next one.
        }
    }
    return null;
}

function persist(mode) {
    const local = store('local');
    if (local) {
        try {
            local.setItem(THEME_KEY, mode);
            return;
        } catch {
            // Fall through to the session-scoped store.
        }
    }
    const session = store('session');
    if (session) {
        try {
            session.setItem(THEME_KEY, mode);
        } catch {
            // No persistence available — apply in-memory only.
        }
    }
}

export function getMode() {
    const raw = readRaw();
    return MODES.includes(raw) ? raw : 'auto';
}

export function resolve(mode) {
    if (mode === 'light' || mode === 'dark') return mode;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function apply(mode = getMode()) {
    const effective = resolve(mode);
    const root = document.documentElement;
    root.setAttribute('data-bs-theme', effective);
    root.setAttribute('data-theme-mode', mode);
    syncToggleUi(mode);
}

export function setMode(mode) {
    const next = MODES.includes(mode) ? mode : 'auto';
    persist(next);
    apply(next);
}

function syncToggleUi(mode) {
    document.querySelectorAll('[data-theme-toggle]').forEach((el) => {
        const value = el.getAttribute('data-theme-value');
        const active = value === mode;
        el.classList.toggle('is-active', active);
        el.setAttribute('aria-checked', active ? 'true' : 'false');
    });
    document.querySelectorAll('[data-theme-icon]').forEach((el) => {
        el.classList.toggle('is-shown', el.getAttribute('data-theme-icon') === mode);
    });
}

export function bindToggles(root = document) {
    root.querySelectorAll('[data-theme-toggle]').forEach((el) => {
        if (el.dataset.themeBound === '1') return;
        el.dataset.themeBound = '1';
        el.addEventListener('click', (event) => {
            event.preventDefault();
            setMode(el.getAttribute('data-theme-value'));
        });
    });
    syncToggleUi(getMode());
}

function onStorage(event) {
    if (event.key !== THEME_KEY && event.key !== null) return;
    apply(getMode());
}

function onSystemThemeChange() {
    if (getMode() === 'auto') apply('auto');
}

window.addEventListener('storage', onStorage);
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', onSystemThemeChange);

/**
 * Blazor enhanced navigation and enhanced form posts re-sync the document from
 * the server-rendered response, which drops runtime-only attributes such as
 * data-bs-theme / data-theme-mode on <html> (the server document does not carry
 * them), silently reverting the page to the light palette until the next full
 * page load. Re-apply the stored theme after every enhanced document update so
 * the palette survives client-side navigation. bindToggles() is idempotent and
 * keeps any re-rendered switcher buttons bound and in sync.
 * Reference: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/navigation
 */
function reapplyAfterEnhancedUpdates() {
    if (typeof window.Blazor?.addEventListener !== 'function') return;
    window.Blazor.addEventListener('enhancedload', () => {
        bindToggles();
        apply();
    });
}

bindToggles();
apply();
reapplyAfterEnhancedUpdates();

/**
 * Auto-flip the theme dropdown when there isn't enough space below the toggle button.
 * Bootstrap 5's Popper.js flip modifier handles this for standard dropdowns, but we
 * removed `dropdown-menu-end` to let Popper decide horizontal alignment. This handler
 * ensures the menu flips upward on mobile / small viewports where the navbar sits near
 * the top of the screen.
 */
function bindDropdownAutoFlip() {
    const root = document.querySelector('[data-theme-root]');
    if (!root) return;

    const toggle = root.querySelector('.dropdown-toggle');
    const menu = root.querySelector('.dropdown-menu');
    if (!toggle || !menu) return;

    // Prevent double-binding after enhanced navigation re-renders
    if (toggle.dataset.themeDropdownBound === '1') return;
    toggle.dataset.themeDropdownBound = '1';

    function adjustPosition() {
        const toggleRect = toggle.getBoundingClientRect();
        const menuHeight = menu.offsetHeight || 120; // fallback estimate before render
        const spaceBelow = window.innerHeight - toggleRect.bottom;
        const spaceAbove = toggleRect.top;
        const margin = 8; // breathing room from viewport edge

        const flipped = spaceBelow < menuHeight + margin && spaceAbove > menuHeight + margin;
        menu.classList.toggle('theme-dropdown-flipped', flipped);
    }

    // Adjust when Bootstrap opens the dropdown
    toggle.addEventListener('show.bs.dropdown', adjustPosition);

    // Re-adjust on resize/orientation change while open
    window.addEventListener('resize', () => {
        if (toggle.getAttribute('aria-expanded') === 'true') {
            adjustPosition();
        }
    });
}

// Bind auto-flip on initial load and after enhanced navigation
bindDropdownAutoFlip();
if (typeof window.Blazor?.addEventListener === 'function') {
    window.Blazor.addEventListener('enhancedload', bindDropdownAutoFlip);
}
