import { createRouter } from "@tanstack/react-router";

import { apiKeysRoute, authRoute } from "./routes/account";
import {
	adminBlockedLinksRoute,
	adminRegistryActivityRoute,
	adminRoute,
	adminUsersRoute,
} from "./routes/admin";
import { publisherRoute, publishersRoute } from "./routes/community";
import { dashboardRoute } from "./routes/dashboard";
import {
	myPackagesRoute,
	packageDocsRoute,
	packageRoute,
	packagesRoute,
} from "./routes/package";
import { homeRoute } from "./routes/public";
import { ErrorPage, NotFoundPage, rootRoute } from "./routes/shared";

export type { RouterContext } from "./routes/shared";

export const clientRoutePaths = [
	"/",
	"/packages",
	"/packages/$packageName",
	"/packages/$packageName/docs",
	"/publishers",
	"/publishers/$publisher",
	"/auth",
	"/dashboard/api-keys",
	"/dashboard/packages/my",
	"/dashboard/admin",
	"/dashboard/admin/users",
	"/dashboard/admin/registry-activity",
	"/dashboard/admin/blocked-links",
] as const;
const routeTree = rootRoute.addChildren([
	homeRoute,
	packagesRoute,
	packageRoute,
	packageDocsRoute,
	publishersRoute,
	publisherRoute,
	authRoute,
	dashboardRoute.addChildren([
		apiKeysRoute,
		myPackagesRoute,
		adminRoute,
		adminRegistryActivityRoute,
		adminBlockedLinksRoute,
		adminUsersRoute,
	]),
]);
export const router = createRouter({
	routeTree,
	context: { queryClient: undefined! },
	defaultErrorComponent: ErrorPage,
	defaultNotFoundComponent: NotFoundPage,
});
declare module "@tanstack/react-router" {
	interface Register {
		router: typeof router;
	}
}
