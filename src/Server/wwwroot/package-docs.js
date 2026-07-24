window.pckgDocs = window.pckgDocs || {
	focusSymbolSearch: () => {
		const el = document.getElementById("package-docs-symbol-search");
		if (el && typeof el.focus === "function") {
			el.focus();
		}
	},

	positionFilterPopover: () => {
		const anchor = document.getElementById("package-docs-filter-button");
		const panel = document.getElementById("package-docs-filter-popover");
		if (!anchor || !panel) {
			return;
		}

		const rect = anchor.getBoundingClientRect();
		const width = Math.min(420, window.innerWidth - 16);
		panel.style.width = `${width}px`;
		panel.style.position = "fixed";
		panel.style.top = `${Math.round(rect.bottom + 6)}px`;
		panel.style.left = `${Math.round(Math.min(rect.left, window.innerWidth - width - 8))}px`;
		panel.style.right = "auto";
		panel.style.zIndex = "1300";
	},
};
