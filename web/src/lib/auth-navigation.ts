export interface DashboardGuardDestination {
	to: "/auth";
	search: { next: string };
}

export function buildAuthHubLoginUrl(authHubUrl: string): string {
	const url = new URL("/login", authHubUrl);
	url.searchParams.set("app", "pckg");
	return url.toString();
}

export function toDashboardGuardDestination(
	next: string,
): DashboardGuardDestination {
	return { to: "/auth", search: { next } };
}
