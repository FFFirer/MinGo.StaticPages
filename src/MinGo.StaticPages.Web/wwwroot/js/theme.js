/**
 * Client-only theme module (ESM).
 * Storage contract (must match the classic inline script in App.razor):
 *   localStorage['mingo-theme'] = 'light' | 'dark' | 'auto'
 * Missing/invalid values resolve as 'auto'.
 */
export const THEME_KEY = 'mingo-theme';
export const MODES = ['light', 'dark', 'auto'];

function safeGetItem(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

function safeSetItem(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        // Private mode / storage disabled — apply theme without persistence.
    }
}

export function getMode() {
    const raw = safeGetItem(THEME_KEY);
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
    safeSetItem(THEME_KEY, next);
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

bindToggles();
apply();
