export interface DashboardGuardDestination {
	to: "/auth";
	search: { next: string };
}

export function buildAuthentikLoginUrl(origin: string, next: string): string {
	const baseUrl = new URL(origin);
	const returnUrl = new URL(next, baseUrl);
	if (returnUrl.origin !== baseUrl.origin) {
		returnUrl.href = new URL("/dashboard/packages/my", baseUrl).href;
	}
	const url = new URL("/outpost.goauthentik.io/start", baseUrl);
	url.searchParams.set("rd", returnUrl.toString());
	return url.toString();
}

export function toDashboardGuardDestination(
	next: string,
): DashboardGuardDestination {
	return { to: "/auth", search: { next } };
}
