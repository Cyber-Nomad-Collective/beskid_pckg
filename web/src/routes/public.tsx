import { buttonVariants } from "@beskid/ui-react/ui/button";
import { createRoute, Link } from "@tanstack/react-router";

import { rootRoute } from "./shared";

export const homeRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/",
	component: HomePage,
});

function HomePage() {
	return (
		<section className="py-12">
			<p className="text-sm font-medium text-primary">Beskid registry</p>
			<h1 className="mt-3 max-w-2xl text-5xl font-bold tracking-tight">
				Publish and discover Beskid packages.
			</h1>
			<p className="mt-5 max-w-xl text-lg text-muted-foreground">
				Browse public packages and manage releases through the Rust registry.
			</p>
			<div className="mt-8 flex flex-wrap gap-3">
				<Link to="/packages" search={{ q: "" }} className={buttonVariants()}>
					Explore packages
				</Link>
			</div>
		</section>
	);
}
