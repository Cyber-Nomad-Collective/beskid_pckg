const STORAGE_KEY = "theme";
const VALID_MODES = new Set(["light", "dark", "system"]);

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
    const effectiveMode = getEffectiveMode(mode);

    document.documentElement.setAttribute("data-app-theme", effectiveMode);

    for (const themeElement of document.querySelectorAll("fluent-design-theme")) {
        themeElement.setAttribute("mode", effectiveMode);
    }
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
    return getEffectiveThemeMode(mode);
}

export function setThemeMode(mode) {
    const storedMode = writeStoredMode(mode);
    applyDocumentTheme(storedMode);
    return storedMode;
}

export function getEffectiveThemeMode(mode) {
    return getEffectiveMode(mode ?? readStoredMode());
}

export function toggleThemeMode(currentMode) {
    const effectiveCurrent = getEffectiveThemeMode(currentMode);
    const nextMode = effectiveCurrent === "dark" ? "light" : "dark";
    return setThemeMode(nextMode);
}
