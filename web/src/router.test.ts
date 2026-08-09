import { describe, expect, it } from "vitest";

import { clientRoutePaths } from "./router";

describe("pckg browser routes", () => {
	it("does not intercept the server-owned Auth Hub finish endpoint", () => {
		expect(clientRoutePaths).not.toContain("/api/auth/hub-finish");
	});

	it("keeps public registry and community entry points available", () => {
		expect(clientRoutePaths).toEqual(
			expect.arrayContaining([
				"/packages/$packageName/docs",
				"/publishers",
				"/publishers/$publisher",
				"/topics",
				"/topics/$topic",
				"/board/post/$postId",
				"/onboarding",
				"/settings/auth/pair",
			]),
		);
	});

	it("keeps only supported dashboard destinations behind the dashboard route", () => {
		expect(
			clientRoutePaths.filter((path) => path.startsWith("/dashboard/")),
		).toEqual(
			expect.arrayContaining([
				"/dashboard/profile",
				"/dashboard/notifications",
				"/dashboard/api-keys",
				"/dashboard/packages/my",
				"/dashboard/packages/upload",
				"/dashboard/admin",
				"/dashboard/admin/users",
				"/dashboard/admin/email",
				"/dashboard/admin/registry-activity",
				"/dashboard/admin/blocked-links",
			]),
		);
	});

	it("does not retain retired C# dashboard routes as browser stubs", () => {
		expect(clientRoutePaths).not.toEqual(
			expect.arrayContaining([
				"/dashboard/packages/all",
			]),
		);
	});
});
