import { Button, buttonVariants } from "@beskid/ui-react/ui/button";
import { Card, CardContent } from "@beskid/ui-react/ui/card";
import { Input } from "@beskid/ui-react/ui/input";
import { useQuery } from "@tanstack/react-query";
import {
	createRoute,
	Link,
	useNavigate,
	useSearch,
} from "@tanstack/react-router";
import { useEffect } from "react";

import { pckgApi } from "../lib/pckg-api";
import { rootRoute } from "./shared";

export const homeRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/",
	component: HomePage,
});

export const onboardingRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/onboarding",
	validateSearch: (search: Record<string, unknown>) => ({
		error: typeof search.error === "string" ? search.error : "",
	}),
	component: OnboardingPage,
});

function OnboardingPage() {
	const { error } = useSearch({ from: "/onboarding" });
	const sessionCheck = useQuery({
		queryKey: ["bootstrap-status"],
		queryFn: () => pckgApi.getBootstrapStatus(),
	});
	if (sessionCheck.isError) {
		throw sessionCheck.error;
	}
	const navigate = useNavigate();

	useEffect(() => {
		if (sessionCheck.data?.hasUsers) {
			void navigate({
				to: "/auth",
				search: { next: "/dashboard/packages/my" },
			});
		}
	}, [sessionCheck.data?.hasUsers, navigate]);

	const message = (() => {
		switch (error) {
			case "missing_credentials":
				return "Display name, email, and password are required.";
			case "missing_name":
				return "Display name is required.";
			case "password_mismatch":
				return "Passwords do not match.";
			case "create_failed":
				return "Unable to create the administrator account.";
			default:
				return "";
		}
	})();

	if (sessionCheck.isPending) {
		return <p className="text-muted-foreground">Checking setup state…</p>;
	}

	return (
		<section className="mx-auto max-w-2xl space-y-4">
			<h1 className="text-3xl font-bold">Welcome</h1>
			<p className="text-muted-foreground">
				Create the first administrator account for this registry.
			</p>
			{message && <p className="text-sm text-destructive">{message}</p>}
			<Card>
				<CardContent className="pt-6">
					<form method="post" action="/onboarding/create" className="space-y-3">
						<Input name="displayName" required placeholder="Your name" type="text" />
						<Input name="email" required placeholder="you@example.com" type="email" />
						<Input
							name="password"
							required
							placeholder="Create a strong password"
							type="password"
						/>
						<Input
							name="confirmPassword"
							required
							placeholder="Repeat password"
							type="password"
						/>
						<Button type="submit">Create administrator</Button>
					</form>
				</CardContent>
			</Card>
		</section>
	);
}

function HomePage() {
	return (
		<section className="py-12">
			<p className="text-sm font-medium text-primary">Beskid registry</p>
			<h1 className="mt-3 max-w-2xl text-5xl font-bold tracking-tight">
				Publish and discover Beskid packages.
			</h1>
			<p className="mt-5 max-w-xl text-lg text-muted-foreground">
				Browse public libraries, follow package discussions, and manage your
				releases through a GitHub-only Beskid Auth Hub identity.
			</p>
			<div className="mt-8 flex flex-wrap gap-3">
				<Link to="/packages" search={{ q: "" }} className={buttonVariants()}>
					Explore packages
				</Link>
				<Link to="/topics" className={buttonVariants({ variant: "outline" })}>
					Visit community
				</Link>
			</div>
		</section>
	);
}
