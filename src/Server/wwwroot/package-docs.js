window.pckgDocs = window.pckgDocs || {
  focusSymbolSearch: function () {
    const el = document.getElementById("package-docs-symbol-search");
    if (el && typeof el.focus === "function") {
      el.focus();
    }
  },
};
