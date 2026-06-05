const STORAGE_KEY = "theme";
const VALID_MODES = new Set(["light", "dark", "system"]);
let activeMode = "system";
let themeObserver = null;

function normalizeMode(mode) {
    if (!mode) {
        return "system";
    }

    const normalized = String(mode).trim().toLowerCase();
    return VALID_MODES.has(normalized) ? normalized : "system";
}

function getSystemMode() {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function getEffectiveMode(mode) {
    const normalized = normalizeMode(mode);
    return normalized === "system" ? getSystemMode() : normalized;
}

function applyDocumentTheme(mode) {
    activeMode = normalizeMode(mode);
    const effectiveMode = getEffectiveMode(activeMode);

    document.documentElement.setAttribute("data-app-theme", effectiveMode);
    // Align hub + Material tokens with site/tracker (data-theme on <html>).
    document.documentElement.setAttribute("data-theme", effectiveMode);

    for (const themeElement of document.querySelectorAll("fluent-design-theme")) {
        themeElement.setAttribute("mode", effectiveMode);
    }
}

function hasThemeInTree(node) {
    if (!(node instanceof Element)) {
        return false;
    }

    if (node.matches("fluent-design-theme")) {
        return true;
    }

    return node.querySelector("fluent-design-theme") !== null;
}

function ensureThemeObserver() {
    if (themeObserver !== null) {
        return;
    }

    themeObserver = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            for (const addedNode of mutation.addedNodes) {
                if (hasThemeInTree(addedNode)) {
                    applyDocumentTheme(activeMode);
                    return;
                }
            }
        }
    });

    themeObserver.observe(document.documentElement, { childList: true, subtree: true });
}

function readStoredMode() {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) {
        return "system";
    }

    try {
        const parsed = JSON.parse(raw);
        if (typeof parsed === "string") {
            return normalizeMode(parsed);
        }

        if (parsed && typeof parsed === "object") {
            return normalizeMode(parsed.mode);
        }
    } catch {
        return normalizeMode(raw);
    }

    return "system";
}

function writeStoredMode(mode) {
    const normalized = normalizeMode(mode);

    let payload = {};
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
        try {
            const parsed = JSON.parse(raw);
            if (parsed && typeof parsed === "object") {
                payload = parsed;
            }
        } catch {
            payload = {};
        }
    }

    payload.mode = normalized;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    return normalized;
}

export function getInitialThemeMode() {
    const mode = readStoredMode();
    applyDocumentTheme(mode);
    ensureThemeObserver();
    return getEffectiveThemeMode(mode);
}

export function setThemeMode(mode) {
    const storedMode = writeStoredMode(mode);
    applyDocumentTheme(storedMode);
    ensureThemeObserver();
    return storedMode;
}

export function getEffectiveThemeMode(mode) {
    return getEffectiveMode(mode ?? readStoredMode());
}

export function getMonacoTheme(mode) {
    return getEffectiveThemeMode(mode) === "light" ? "vs" : "vs-dark";
}

export function toggleThemeMode(currentMode) {
    const effectiveCurrent = getEffectiveThemeMode(currentMode);
    const nextMode = effectiveCurrent === "dark" ? "light" : "dark";
    return setThemeMode(nextMode);
}
