import { describe, expect, it } from "vitest";

import {
	buildAuthHubLoginUrl,
	toDashboardGuardDestination,
} from "./auth-navigation";

describe("pckg authentication navigation", () => {
	it("redirects sign-in to the Auth Hub with the pckg app identifier", () => {
		expect(buildAuthHubLoginUrl("https://auth.beskid.test/")).toBe(
			"https://auth.beskid.test/login?app=pckg",
		);
	});

	it("sends unauthenticated dashboard visitors through Auth Hub", () => {
		expect(toDashboardGuardDestination("/dashboard/packages/my")).toEqual({
			to: "/auth",
			search: { next: "/dashboard/packages/my" },
		});
	});
});
