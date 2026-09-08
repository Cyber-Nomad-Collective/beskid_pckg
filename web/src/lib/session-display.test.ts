import { describe, expect, it } from "vitest";

import { sessionDisplayName } from "./session-display";

describe("sessionDisplayName", () => {
	it("prefers the Authentik display name", () => {
		expect(
			sessionDisplayName({
				subject: "pmikstacki",
				displayName: "Piotr Mikstacki",
				email: "piotr@example.test",
				groups: ["Admins"],
			}),
		).toBe("Piotr Mikstacki");
	});

	it("falls back to the Authentik subject", () => {
		expect(
			sessionDisplayName({
				subject: "pmikstacki",
				displayName: null,
				email: null,
				groups: [],
			}),
		).toBe("pmikstacki");
	});
});
