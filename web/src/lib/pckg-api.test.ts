import { describe, expect, it } from "vitest";

import { PckgApiClient } from "./pckg-api";

describe("PckgApiClient", () => {
	it("uses the registry search endpoint for a package query", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json([{ package: { id: "1", name: "beskid.compiler", description: "Compiler", tags: ["tooling"] } }]);
			},
		});

		await expect(client.listPackages({ query: "compiler" })).resolves.toMatchObject([{ name: "beskid.compiler" }]);
		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/search");
		expect(new URL(requests[0].url, "https://pckg.test").search).toBe("?q=compiler");
	});

	it("loads the registry index without using a search wrapper", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json([{ id: "1", name: "beskid.compiler", description: "Compiler", tags: [] }]);
			},
		});

		await expect(client.listPackages()).resolves.toMatchObject([{ name: "beskid.compiler" }]);
		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/packages");
	});

	it("loads package details with metadata and a latest download URL", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				requests.push(new Request(request));
				return Response.json({
					package: { id: "1", name: "beskid.compiler", description: "Compiler", category: "Tooling", repositoryUrl: "https://github.com/beskid-lang/compiler", websiteUrl: "https://beskid-lang.org", tags: [], totalDownloads: 42, updatedAtUtc: "2026-01-01T00:00:00Z", ownerDisplayName: "Beskid" },
					versions: [{ id: "v1", version: "1.2.3", publishedAtUtc: "2026-01-01T00:00:00Z", isYanked: false, checksumSha256: "abc", sizeBytes: 1234, hasReadme: true }],
					dependencies: [], dependentsCount: 4, readme: "# Compiler", latestVersion: "1.2.3",
				});
			},
		});

		const details = await client.getPackage("beskid.compiler");
		expect(new URL(requests[0].url, "https://pckg.test").pathname).toBe("/api/packages/beskid.compiler");
		expect(details.package.repositoryUrl).toBe("https://github.com/beskid-lang/compiler");
		expect(details.latestDownloadUrl).toBe("http://localhost/api/packages/beskid.compiler/versions/latest/download");
		expect(details.versions[0].hasReadme).toBe(true);
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

	it("loads versioned artifact documentation and source through the read-only browse endpoints", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				const captured = new Request(request);
				requests.push(captured);
				if (captured.url.includes("/docs/structured")) return Response.json({ readme: "# Demo", metadata: { title: "Demo" } });
				if (captured.url.endsWith("/docs") || captured.url.includes("/source/tree")) return Response.json([{ path: "docs/guide.md", sizeBytes: 12 }]);
				return new Response("module Demo", { headers: { "content-type": "text/plain" } });
			},
		});

		await expect(client.getPackageReadme("beskid.demo", "1.2.3")).resolves.toBe("module Demo");
		await expect(client.listPackageDocs("beskid.demo", "1.2.3")).resolves.toEqual([{ path: "docs/guide.md", sizeBytes: 12 }]);
		await expect(client.getPackageDoc("beskid.demo", "1.2.3", "docs/guide.md")).resolves.toBe("module Demo");
		await expect(client.getStructuredPackageDocs("beskid.demo", "1.2.3")).resolves.toMatchObject({ metadata: { title: "Demo" } });
		await expect(client.listPackageSource("beskid.demo", "1.2.3")).resolves.toEqual([{ path: "docs/guide.md", sizeBytes: 12 }]);
		await expect(client.getPackageSource("beskid.demo", "1.2.3", "src/main.bsk")).resolves.toBe("module Demo");

		expect(requests.map((request) => `${request.method} ${new URL(request.url, "https://pckg.test").pathname}${new URL(request.url, "https://pckg.test").search}`)).toEqual([
			"GET /api/packages/beskid.demo/versions/1.2.3/readme",
			"GET /api/packages/beskid.demo/versions/1.2.3/docs",
			"GET /api/packages/beskid.demo/versions/1.2.3/docs/file?path=docs%2Fguide.md",
			"GET /api/packages/beskid.demo/versions/1.2.3/docs/structured",
			"GET /api/packages/beskid.demo/versions/1.2.3/source/tree",
			"GET /api/packages/beskid.demo/versions/1.2.3/source/file?path=src%2Fmain.bsk",
		]);
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

	it("uses typed community read and interaction endpoints", async () => {
		const requests: Request[] = [];
		const client = new PckgApiClient({
			fetch: async (request) => {
				const captured = new Request(request);
				requests.push(captured);
				return Response.json([]);
			},
		});

		await client.listBoards();
		await client.getBoard("general");
		await client.listBoardPosts("general");
		await client.getPost(7);
		await client.listPostComments(7);
		await client.togglePublisherFollow("github:42");
		await client.voteOnPost(7, 1);
		await client.createComment(7, { content: "Useful package." });

		expect(requests.map((request) => `${request.method} ${new URL(request.url, "https://pckg.test").pathname}`)).toEqual([
			"GET /api/community/boards",
			"GET /api/community/boards/general",
			"GET /api/community/boards/general/posts",
			"GET /api/community/boards/posts/7",
			"GET /api/community/boards/posts/7/comments",
			"POST /api/community/publisher-follows/github%3A42/toggle",
			"POST /api/community/boards/posts/7/vote",
			"POST /api/community/boards/posts/7/comments",
		]);
		expect(await requests[6].text()).toBe('{"value":1}');
		expect(await requests[7].text()).toBe('{"content":"Useful package."}');
	});
});
