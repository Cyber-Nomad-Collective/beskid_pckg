import path from "node:path";
import { fileURLToPath } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
	plugins: [react(), tailwindcss()],
	resolve: {
		// file: @beskid/* packages declare react as a peer; dedupe so Vite/Rolldown
		// does not stub them as __vite-optional-peer-dep.
		dedupe: ["react", "react-dom"],
		alias: {
			"@beskid/material-theme": path.resolve(
				path.dirname(fileURLToPath(import.meta.url)),
				"node_modules/@beskid/beskid-ui/src/styles/theme.material.css",
			),
		},
	},
});
