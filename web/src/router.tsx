import { createRouter } from "@tanstack/react-router";

import {
	apiKeysRoute,
	authRoute,
	notificationsRoute,
	profileRoute,
	settingsAuthPairRoute,
} from "./routes/account";
import {
	adminBlockedLinksRoute,
	adminEmailRoute,
	adminRegistryActivityRoute,
	adminRoute,
	adminUsersRoute,
	boardModerationRoute,
} from "./routes/admin";
import {
	boardPostRoute,
	publisherRoute,
	publishersRoute,
	topicRoute,
	topicsRoute,
} from "./routes/community";
import { dashboardRoute } from "./routes/dashboard";
import {
	myPackagesRoute,
	packageDocsRoute,
	packageRoute,
	packagesRoute,
	packageUploadRoute,
} from "./routes/package";
import { homeRoute, onboardingRoute } from "./routes/public";
import { ErrorPage, NotFoundPage, rootRoute } from "./routes/shared";

export type { RouterContext } from "./routes/shared";

export const clientRoutePaths = [
	"/",
	"/onboarding",
	"/packages",
	"/packages/$packageName",
	"/packages/$packageName/docs",
	"/publishers",
	"/publishers/$publisher",
	"/topics",
	"/topics/$topic",
	"/board/post/$postId",
	"/auth",
	"/settings/auth/pair",
	"/dashboard/profile",
	"/dashboard/notifications",
	"/dashboard/api-keys",
	"/dashboard/packages/my",
	"/dashboard/packages/upload",
	"/dashboard/admin",
	"/dashboard/admin/users",
	"/dashboard/admin/email",
	"/dashboard/admin/registry-activity",
	"/dashboard/admin/blocked-links",
	"/dashboard/admin/boards",
] as const;
const routeTree = rootRoute.addChildren([
	homeRoute,
	onboardingRoute,
	packagesRoute,
	packageRoute,
	packageDocsRoute,
	publishersRoute,
	publisherRoute,
	topicsRoute,
	topicRoute,
	boardPostRoute,
	authRoute,
	settingsAuthPairRoute,
	dashboardRoute.addChildren([
		profileRoute,
		notificationsRoute,
		apiKeysRoute,
		myPackagesRoute,
		packageUploadRoute,
		adminRoute,
		adminEmailRoute,
		adminRegistryActivityRoute,
		adminBlockedLinksRoute,
		adminUsersRoute,
		boardModerationRoute,
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
