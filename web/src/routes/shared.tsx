import { BeskidHub } from "@beskid/beskid-ui/react/BeskidHub";
import { buttonVariants } from "@beskid/ui-react/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import type { QueryClient } from "@tanstack/react-query";
import {
	createRootRouteWithContext,
	Link,
	Outlet,
} from "@tanstack/react-router";

export interface RouterContext {
	queryClient: QueryClient;
}

function AppShell() {
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
						to="/topics"
						className="text-sm text-muted-foreground hover:text-foreground"
					>
						Community
					</Link>
					<Link
						to="/publishers"
						className="text-sm text-muted-foreground hover:text-foreground"
					>
						Publishers
					</Link>
					<div className="ml-auto flex gap-2">
						<Link
							to="/auth"
							search={{ next: "/dashboard/packages/my" }}
							className={buttonVariants({ variant: "outline" })}
						>
							Sign in
						</Link>
						<Link to="/dashboard/packages/my" className={buttonVariants()}>
							Dashboard
						</Link>
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
