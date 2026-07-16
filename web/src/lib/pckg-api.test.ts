import { describe, expect, it } from "vitest";

import { PckgApiClient } from "./pckg-api";

describe("PckgApiClient", () => {
	it("loads packages through the implemented registry endpoint and filters them locally", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json([{ id: "1", name: "beskid.compiler", description: "Compiler", tags: ["tooling"] }]);
			},
		});

		await expect(client.listPackages({ query: "compiler" })).resolves.toMatchObject([{ name: "beskid.compiler" }]);
		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/packages");
		expect(new URL(requests[0].url, "https://pckg.test").search).toBe("");
	});

	it("keeps browser session requests credentialed and reports unauthenticated users", async () => {
		const client = new PckgApiClient({
			fetch: async (request) => {
				expect(new Request(request).credentials).toBe("include");
				return new Response(null, { status: 401 });
			},
		});

		await expect(client.getSession()).resolves.toBeNull();
	});

	it("uploads an authenticated raw package artifact to its version endpoint", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json({ version: "1.2.3" }, { status: 201 });
			},
		});

		await expect(client.publishPackage({
			packageName: "beskid.http",
			version: "1.2.3",
			artifact: new File(["package"], "beskid.http-1.2.3.bpk", { type: "application/zip" }),
		})).resolves.toEqual({ version: "1.2.3" });

		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/packages/beskid.http/versions/1.2.3/artifact");
		expect(requests[0].credentials).toBe("include");
		expect(requests[0].method).toBe("POST");
		expect(requests[0].headers.get("content-type")).toBe("application/zip");
		expect(await requests[0].text()).toBe("package");
		expect(requests).toHaveLength(1);
	});

	it("builds a version-specific package download URL", () => {
		const client = new PckgApiClient({ fetch: async () => Response.json({}) });

		expect(client.packageDownloadUrl("beskid/http", "1.2.3")).toBe(
			"http://localhost/api/packages/beskid%2Fhttp/versions/1.2.3/download",
		);
	});

	it("uses the implemented community profile and notification contracts", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				const captured = new Request(request);
				requests.push(captured);
				return captured.url.includes("notification-preferences") ? new Response(null, { status: 204 }) : Response.json([]);
			},
		});

		await client.getCommunityProfile("github:42");
		await client.updateMyCommunityProfile({ display_name: "Ada", bio: "Compiler author", social_links: ["https://example.test"] });
		await client.listNotifications();
		await client.updateNotificationPreference("mentionsOnly");

		expect(requests.map((request) => new URL(request.url, "https://pckg.test").pathname)).toEqual([
			"/api/community/profiles/github%3A42",
			"/api/community/profiles/me",
			"/api/community/notifications",
			"/api/community/notification-preferences",
		]);
		expect(requests[1].method).toBe("PUT");
		expect(await requests[1].text()).toBe('{"displayName":"Ada","bio":"Compiler author","socialLinks":["https://example.test"]}');
	});
});
