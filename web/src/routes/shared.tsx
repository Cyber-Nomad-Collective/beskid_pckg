import { BeskidHub } from "@beskid/beskid-ui/react/BeskidHub";
import { buttonVariants } from "@beskid/ui-react/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import { useQuery, type QueryClient } from "@tanstack/react-query";
import {
	createRootRouteWithContext,
	Link,
	Outlet,
} from "@tanstack/react-router";

import { pckgApi } from "../lib/pckg-api";
import { sessionDisplayName } from "../lib/session-display";

export interface RouterContext {
	queryClient: QueryClient;
}

function AppShell() {
	const session = useQuery({
		queryKey: ["session"],
		queryFn: () => pckgApi.getSession(),
		staleTime: 60_000,
	});
	const account = session.data;
	const accountLabel = account ? sessionDisplayName(account) : null;
	const initials = accountLabel
		?.split(/\s+/)
		.map((part) => part[0])
		.join("")
		.slice(0, 2)
		.toUpperCase();
	return (
		<div className="min-h-screen">
			<header className="border-b border-border">
				<nav
					className="mx-auto flex max-w-6xl items-center gap-5 px-5 py-3"
					aria-label="Primary navigation"
				>
					<BeskidHub />
					<Link to="/" className="font-semibold tracking-tight">
						pckg
					</Link>
					<Link
						to="/packages"
						search={{ q: "" }}
						className="text-sm text-muted-foreground hover:text-foreground"
					>
						Packages
					</Link>
					<Link
						to="/packages"
						search={{ q: "" }}
						className="text-sm text-muted-foreground hover:text-foreground"
					>
						Package docs
					</Link>
					<Link
						to="/publishers"
						className="text-sm text-muted-foreground hover:text-foreground"
					>
						Publishers
					</Link>
					<div className="ml-auto flex items-center gap-2">
						{session.isPending ? (
							<span className="text-sm text-muted-foreground">Checking session…</span>
						) : account && accountLabel ? (
							<details className="relative">
								<summary className="flex cursor-pointer list-none items-center gap-2 rounded-full border border-border px-2 py-1 text-sm font-medium hover:bg-muted">
									<span className="flex size-6 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
										{initials}
									</span>
									<span>{accountLabel}</span>
								</summary>
								<div className="absolute right-0 z-10 mt-2 w-56 rounded-lg border border-border bg-background p-2 shadow-lg">
									<p className="px-2 py-1 text-xs text-muted-foreground">Signed in through Authentik</p>
									<Link to="/dashboard/packages/my" className="block rounded px-2 py-1.5 text-sm hover:bg-muted">
										Dashboard
									</Link>
									<a href="https://auth.beskid-lang.org/if/user/" className="block rounded px-2 py-1.5 text-sm hover:bg-muted">
										Manage account
									</a>
									<a href="/outpost.goauthentik.io/sign_out" className="block rounded px-2 py-1.5 text-sm text-destructive hover:bg-muted">
										Log out
									</a>
								</div>
							</details>
						) : (
							<Link to="/auth" search={{ next: "/dashboard/packages/my" }} className={buttonVariants({ variant: "outline" })}>
								Sign in
							</Link>
						)}
					</div>
				</nav>
			</header>
			<main className="mx-auto max-w-6xl px-5 py-10">
				<Outlet />
			</main>
		</div>
	);
}

export function ErrorPage({ error }: { error: unknown }) {
	return (
		<Card className="mx-auto max-w-xl">
			<CardHeader>
				<CardTitle>Something went wrong</CardTitle>
				<CardDescription>
					{error instanceof Error ? error.message : "Please try again."}
				</CardDescription>
			</CardHeader>
			<CardContent>
				<Link to="/" className={buttonVariants()}>
					Return home
				</Link>
			</CardContent>
		</Card>
	);
}
export function NotFoundPage() {
	return (
		<Card className="mx-auto max-w-xl">
			<CardHeader>
				<CardTitle>Page not found</CardTitle>
				<CardDescription>
					The registry page you requested does not exist.
				</CardDescription>
			</CardHeader>
			<CardContent>
				<Link to="/packages" search={{ q: "" }} className={buttonVariants()}>
					Browse packages
				</Link>
			</CardContent>
		</Card>
	);
}

export const rootRoute = createRootRouteWithContext<RouterContext>()({
	component: AppShell,
	errorComponent: ErrorPage,
	notFoundComponent: NotFoundPage,
});
