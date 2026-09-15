import { Badge } from "@beskid/ui-react/ui/badge";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import { useQuery } from "@tanstack/react-query";
import { createRoute, Link, useParams } from "@tanstack/react-router";

import { pckgApi } from "../lib/pckg-api";
import { rootRoute } from "./shared";

export const publishersRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/publishers",
	component: PublishersPage,
});

function PublishersPage() {
	const publishers = useQuery({
		queryKey: ["publishers"],
		queryFn: () => pckgApi.listPublishers(),
	});
	if (publishers.isPending)
		return <p className="text-muted-foreground">Loading publishers…</p>;
	if (publishers.isError) throw publishers.error;
	return (
		<section>
			<header>
				<h1 className="text-3xl font-bold">Publishers</h1>
				<p className="mt-2 text-muted-foreground">
					Owners of public packages in this registry.
				</p>
			</header>
			<div className="mt-6 grid gap-4 md:grid-cols-2">
				{publishers.data.length === 0 ? (
					<Card className="md:col-span-2">
						<CardContent className="py-8 text-muted-foreground">
							No public publishers yet.
						</CardContent>
					</Card>
				) : (
					publishers.data.map((publisher) => (
						<Card key={publisher.subject}>
							<CardHeader>
								<CardTitle className="flex items-center gap-2">
									<Link
										to="/publishers/$publisher"
										params={{ publisher: publisher.subject }}
										className="hover:underline"
									>
										{publisher.displayName}
									</Link>
									{publisher.isPublisherVerified && <Badge>Verified</Badge>}
								</CardTitle>
								<CardDescription>{publisher.subject}</CardDescription>
							</CardHeader>
							<CardContent className="text-sm text-muted-foreground">
								{publisher.packageCount} public package
								{publisher.packageCount === 1 ? "" : "s"}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}

export const publisherRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/publishers/$publisher",
	component: PublisherPage,
});

function PublisherPage() {
	const { publisher } = useParams({ from: "/publishers/$publisher" });
	const packages = useQuery({
		queryKey: ["publisher-packages", publisher],
		queryFn: () => pckgApi.listPublisherPackages(publisher),
	});
	if (packages.isPending)
		return <p className="text-muted-foreground">Loading published packages…</p>;
	if (packages.isError) throw packages.error;
	return (
		<section>
			<h1 className="text-3xl font-bold">{publisher}</h1>
			<p className="mt-3 text-muted-foreground">
				Public packages owned by this registry subject.
			</p>
			<div className="mt-6 grid gap-4 md:grid-cols-2">
				{packages.data.length === 0 ? (
					<Card className="md:col-span-2">
						<CardContent className="py-6 text-muted-foreground">
							This publisher has no public packages.
						</CardContent>
					</Card>
				) : (
					packages.data.map((item) => (
						<Card key={item.id}>
							<CardHeader>
								<CardTitle>
									<Link
										to="/packages/$packageName"
										params={{ packageName: item.name }}
										className="hover:underline"
									>
										{item.name}
									</Link>
								</CardTitle>
								<CardDescription>{item.description}</CardDescription>
							</CardHeader>
							<CardContent className="text-sm text-muted-foreground">
								{item.category} · {item.totalDownloads.toLocaleString()} downloads
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}
