export interface PackageSummary {
	id: string;
	name: string;
	description: string;
	category: string;
	tags: string[];
	totalDownloads: number;
	updatedAtUtc: string;
	ownerDisplayName: string;
}

export interface PackageDetails {
	package: PackageSummary;
	versions: Array<{ id: string; version: string; publishedAtUtc: string; isYanked: boolean; checksumSha256: string; sizeBytes: number }>;
	readme: string | null;
	latestVersion: string | null;
}

export interface Session {
	subject: string;
	githubLogin: string;
	hubSessionId: string;
}

export interface PublishPackageInput {
	packageName: string;
	version: string;
	artifact: File;
}

export interface PublishedPackageVersion {
	version: string;
}

export interface SearchPackagesInput {
	query?: string;
}

export interface CommunityProfile {
	subject: string;
	display_name: string;
	bio: string;
	social_links: string[];
}

export interface Notification {
	recipient: string;
	scope: string;
	actor: string;
	post_id: number | null;
	comment_id: number | null;
}

export class PckgApiClient {
	private readonly fetch: typeof globalThis.fetch;
	private readonly baseUrl: string;

	constructor(options: { fetch?: typeof globalThis.fetch; baseUrl?: string } = {}) {
		this.fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
		this.baseUrl = options.baseUrl ?? globalThis.location?.origin ?? "http://localhost";
	}

	async listPackages(input: SearchPackagesInput = {}): Promise<PackageSummary[]> {
		const packages = await this.get<PackageSummary[]>("/api/packages");
		const query = input.query?.trim().toLocaleLowerCase();
		return query ? packages.filter((item) => [item.name, item.description, ...item.tags].join(" ").toLocaleLowerCase().includes(query)) : packages;
	}

	async getPackage(packageName: string): Promise<PackageDetails> {
		return this.get<PackageDetails>(`/api/packages/${encodeURIComponent(packageName)}`);
	}

	async getSession(): Promise<Session | null> {
		const response = await this.request("/api/auth/session");
		if (response.status === 401) return null;
		return this.readJson<Session>(response);
	}

	async publishPackage(input: PublishPackageInput): Promise<PublishedPackageVersion> {
		const packageName = encodeURIComponent(input.packageName);
		const version = input.version.trim();
		return this.readJson<PublishedPackageVersion>(await this.request(
			`/api/packages/${packageName}/versions/${encodeURIComponent(version)}/artifact`,
			{ method: "POST", headers: { "Content-Type": input.artifact.type || "application/zip" }, body: input.artifact },
		));
	}

	async getCommunityProfile(subject: string): Promise<CommunityProfile> {
		return this.get<CommunityProfile>(`/api/community/profiles/${encodeURIComponent(subject)}`);
	}

	async updateMyCommunityProfile(input: Pick<CommunityProfile, "display_name" | "bio" | "social_links">): Promise<CommunityProfile> {
		return this.readJson<CommunityProfile>(await this.request("/api/community/profiles/me", {
			method: "PUT",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ displayName: input.display_name, bio: input.bio, socialLinks: input.social_links }),
		}));
	}

	async listNotifications(): Promise<Notification[]> {
		return this.get<Notification[]>("/api/community/notifications");
	}

	async updateNotificationPreference(mode: "all" | "mentionsOnly"): Promise<void> {
		const response = await this.request("/api/community/notification-preferences", {
			method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ mode }),
		});
		if (!response.ok) throw new PckgApiError(response.status);
	}

	packageDownloadUrl(packageName: string, version: string): string {
		return `${this.baseUrl}/api/packages/${encodeURIComponent(packageName)}/versions/${encodeURIComponent(version)}/download`;
	}

	private async get<T>(path: string): Promise<T> {
		return this.readJson<T>(await this.request(path));
	}

	private request(path: string, init?: RequestInit): Promise<Response> {
		return this.fetch(new Request(`${this.baseUrl}${path}`, { ...init, credentials: "include" }));
	}

	private async readJson<T>(response: Response): Promise<T> {
		if (!response.ok) throw new PckgApiError(response.status);
		return response.json() as Promise<T>;
	}
}

export class PckgApiError extends Error {
	constructor(readonly status: number) {
		super(`pckg API request failed with status ${status}`);
	}
}

export const pckgApi = new PckgApiClient();
