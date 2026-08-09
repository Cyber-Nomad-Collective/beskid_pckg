import { createRoute, Link, Outlet, redirect } from "@tanstack/react-router";

import { toDashboardGuardDestination } from "../lib/auth-navigation";
import { pckgApi } from "../lib/pckg-api";
import { rootRoute } from "./shared";

export const dashboardRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/dashboard",
	beforeLoad: async ({ location }) => {
		if (await pckgApi.getSession()) return;
		throw redirect(toDashboardGuardDestination(location.pathname));
	},
	component: DashboardLayout,
});
function DashboardLayout() {
	return (
		<div className="grid gap-8 lg:grid-cols-[13rem_1fr]">
			<aside className="space-y-1 rounded-lg border border-border p-3">
				<p className="px-2 pb-2 text-sm font-semibold">Dashboard</p>
				{[
					["/dashboard/profile", "Profile"],
					["/dashboard/notifications", "Notifications"],
					["/dashboard/api-keys", "API keys"],
					["/dashboard/packages/my", "My packages"],
					["/dashboard/packages/upload", "Upload package"],
					["/dashboard/admin", "Administration"],
					["/dashboard/admin/email", "Email settings"],
					["/dashboard/admin/registry-activity", "Registry activity"],
					["/dashboard/admin/blocked-links", "Blocked links"],
				].map(([to, label]) => (
					<Link
						key={to}
						to={to}
						className="block rounded px-2 py-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground"
					>
						{label}
					</Link>
				))}
			</aside>
			<section>
				<Outlet />
			</section>
		</div>
	);
}
