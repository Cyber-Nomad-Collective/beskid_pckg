import { describe, expect, it } from "vitest";

import { clientRoutePaths } from "./router";

describe("pckg browser routes", () => {
	it("exposes only pages backed by the Rust registry", () => {
		expect(clientRoutePaths).toEqual([
			"/",
			"/packages",
			"/packages/$packageName",
			"/packages/$packageName/docs",
			"/publishers",
			"/publishers/$publisher",
			"/auth",
			"/dashboard/api-keys",
			"/dashboard/packages/my",
			"/dashboard/admin",
			"/dashboard/admin/users",
			"/dashboard/admin/registry-activity",
			"/dashboard/admin/blocked-links",
		]);
	});
});
