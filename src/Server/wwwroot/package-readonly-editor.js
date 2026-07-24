/** @type {WeakMap<Element, { editorId: string, ro: ResizeObserver | null, onWindowResize: () => void, raf: number }>} */
const hosts = new WeakMap();

const MIN_HEIGHT_PX = 280;

/**
 * @param {HTMLElement | null | undefined} shell
 * @returns {HTMLElement | null}
 */
function previewHost(shell) {
	return (
		shell?.closest(".source-preview-host") ??
		shell?.closest(".source-preview-panel") ??
		shell?.parentElement ??
		null
	);
}

/**
 * Measure the preview region and apply explicit pixel width/height to the Monaco shell.
 * @param {HTMLElement} shell
 * @param {string} editorId
 */
export function layoutNow(shell, editorId) {
	if (!shell) {
		return;
	}

	const host = previewHost(shell);
	let width = shell.parentElement?.clientWidth ?? shell.clientWidth;
	let height = MIN_HEIGHT_PX;

	if (host) {
		const rect = host.getBoundingClientRect();
		width = rect.width;
		height = Math.max(MIN_HEIGHT_PX, rect.height);

		const panel = host.closest(".source-preview-panel");
		if (panel && host.previousElementSibling instanceof HTMLElement) {
			const panelRect = panel.getBoundingClientRect();
			const headerH = host.previousElementSibling.getBoundingClientRect().height;
			const gap = 10;
			height = Math.max(
				MIN_HEIGHT_PX,
				Math.floor(panelRect.height - headerH - gap),
			);
		}
	}

	width = Math.max(0, Math.floor(width));
	height = Math.max(MIN_HEIGHT_PX, Math.floor(height));

	shell.style.width = `${width}px`;
	shell.style.height = `${height}px`;

	const monaco = globalThis.blazorMonaco?.editor;
	if (monaco?.layout) {
		try {
			monaco.layout(editorId, { width, height }, false);
		} catch {
			// Editor may not be mounted yet; a later resize will retry.
		}
	}
}

/**
 * @param {HTMLElement} shell
 * @param {string} editorId
 */
export function attach(shell, editorId) {
	if (!shell) {
		return;
	}

	dispose(shell);

	const state = {
		editorId,
		ro: null,
		onWindowResize: null,
		raf: 0,
	};

	const schedule = (target, id) => {
		cancelAnimationFrame(state.raf);
		state.raf = requestAnimationFrame(() => layoutNow(target, id));
	};

	state.onWindowResize = () => schedule(shell, editorId);
	state.ro = new ResizeObserver(() => schedule(shell, editorId));
	const observeTargets = new Set();
	for (const el of [
		previewHost(shell),
		shell.closest(".source-preview-panel"),
		shell.closest(".source-browser-layout"),
		shell.parentElement,
	]) {
		if (el instanceof HTMLElement && !observeTargets.has(el)) {
			observeTargets.add(el);
			state.ro.observe(el);
		}
	}

	window.addEventListener("resize", state.onWindowResize);
	hosts.set(shell, state);
	schedule(shell, editorId);
}

/**
 * @param {HTMLElement} shell
 */
export function dispose(shell) {
	const state = hosts.get(shell);
	if (!state) {
		return;
	}

	window.removeEventListener("resize", state.onWindowResize);
	state.ro?.disconnect();
	cancelAnimationFrame(state.raf);
	hosts.delete(shell);
}
