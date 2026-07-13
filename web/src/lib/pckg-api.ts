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

export interface PackageSearchResult {
	package: PackageSummary;
	reviewCount: number;
}

export interface PackageDetails {
	package: PackageSummary;
	versions: Array<{ version: string; publishedAtUtc: string; isYanked: boolean }>;
	readme: string | null;
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
	limit?: number;
}

export class PckgApiClient {
	private readonly fetch: typeof globalThis.fetch;
	private readonly baseUrl: string;

	constructor(options: { fetch?: typeof globalThis.fetch; baseUrl?: string } = {}) {
		this.fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
		this.baseUrl = options.baseUrl ?? globalThis.location?.origin ?? "http://localhost";
	}

	async searchPackages(input: SearchPackagesInput = {}): Promise<PackageSearchResult[]> {
		const query = new URLSearchParams();
		if (input.query) query.set("q", input.query);
		if (input.limit) query.set("limit", String(input.limit));
		return this.get<PackageSearchResult[]>(`/api/search${query.size ? `?${query}` : ""}`);
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
