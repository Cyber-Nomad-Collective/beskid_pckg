import { describe, expect, it } from "vitest";

import {
	buildAuthentikLoginUrl,
	toDashboardGuardDestination,
} from "./auth-navigation";

describe("pckg authentication navigation", () => {
	it("starts sign-in at the local Authentik outpost with a safe return URL", () => {
		expect(buildAuthentikLoginUrl("https://pckg.beskid.test", "/dashboard/packages/my")).toBe(
			"https://pckg.beskid.test/outpost.goauthentik.io/start?rd=https%3A%2F%2Fpckg.beskid.test%2Fdashboard%2Fpackages%2Fmy",
		);
	});

	it("does not allow an external return URL", () => {
		expect(buildAuthentikLoginUrl("https://pckg.beskid.test", "https://untrusted.example/path")).toBe(
			"https://pckg.beskid.test/outpost.goauthentik.io/start?rd=https%3A%2F%2Fpckg.beskid.test%2Fdashboard%2Fpackages%2Fmy",
		);
	});

	it("sends unauthenticated dashboard visitors through the Authentik fallback", () => {
		expect(toDashboardGuardDestination("/dashboard/packages/my")).toEqual({
			to: "/auth",
			search: { next: "/dashboard/packages/my" },
		});
	});
});
