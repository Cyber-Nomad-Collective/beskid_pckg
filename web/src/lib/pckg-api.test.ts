import { describe, expect, it } from "vitest";

import { PckgApiClient } from "./pckg-api";

describe("PckgApiClient", () => {
	it("loads public packages through the canonical search endpoint", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json([]);
			},
		});

		await expect(client.searchPackages({ query: "compiler", limit: 12 })).resolves.toEqual([]);
		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/search");
		expect(new URL(requests[0].url, "https://pckg.test").search).toBe("?q=compiler&limit=12");
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
});
