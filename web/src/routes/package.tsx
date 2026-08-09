import { Badge } from "@beskid/ui-react/ui/badge";
import { Button, buttonVariants } from "@beskid/ui-react/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import { Input } from "@beskid/ui-react/ui/input";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
	createRoute,
	Link,
	useNavigate,
	useParams,
	useSearch,
} from "@tanstack/react-router";
import { useState } from "react";

import { PackageSourceGraphPanel } from "../components/package-source-graph-panel";
import { toDashboardGuardDestination } from "../lib/auth-navigation";
import { PckgApiError, pckgApi } from "../lib/pckg-api";
import { dashboardRoute } from "./dashboard";
import { rootRoute } from "./shared";

export const packagesRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/packages",
	validateSearch: (search: Record<string, unknown>) => ({
		q: typeof search.q === "string" ? search.q : "",
	}),
	component: PackagesPage,
});
function PackagesPage() {
	const { q } = useSearch({ from: "/packages" });
	const packages = useQuery({
		queryKey: ["packages", q],
		queryFn: () => pckgApi.listPackages({ query: q || undefined }),
	});
	if (packages.isPending)
		return <p className="text-muted-foreground">Loading packages…</p>;
	if (packages.isError) throw packages.error;
	return (
		<section>
			<div className="flex flex-wrap items-end justify-between gap-4">
				<div>
					<h1 className="text-3xl font-bold">Packages</h1>
					<p className="mt-2 text-muted-foreground">
						Find public packages for your Beskid projects.
					</p>
				</div>
				<form className="flex gap-2" action="/packages">
					<Input
						name="q"
						defaultValue={q}
						placeholder="Search packages"
						aria-label="Search packages"
					/>
					<Button type="submit" variant="outline">
						Search
					</Button>
				</form>
			</div>
			<div className="mt-6 grid gap-4 md:grid-cols-2">
				{packages.data.length === 0 ? (
					<Card className="md:col-span-2">
						<CardContent className="py-8 text-muted-foreground">
							No packages match this search.
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
							<CardContent className="flex flex-wrap gap-2 text-sm text-muted-foreground">
								<span>{item.category}</span>
								<span>·</span>
								<span>{item.totalDownloads.toLocaleString()} downloads</span>
								<span>·</span>
								<span>{item.ownerDisplayName}</span>
								{item.tags.slice(0, 3).map((tag) => (
									<Badge key={tag} variant="secondary">
										{tag}
									</Badge>
								))}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}

export const packageRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/packages/$packageName",
	component: PackageDetailsPage,
});
function PackageDetailsPage() {
	const { packageName } = useParams({ from: "/packages/$packageName" });
	const details = useQuery({
		queryKey: ["package", packageName],
		queryFn: () => pckgApi.getPackage(packageName),
	});
	const reviews = useQuery({
		queryKey: ["package-community-reviews", packageName],
		queryFn: () => pckgApi.listPackageCommunityReviews(packageName),
	});
	const queryClient = useQueryClient();
	const submitReview = useMutation({
		mutationFn: (input: { rating: number; comment: string }) =>
			pckgApi.createPackageCommunityReview(packageName, input),
		onSuccess: () =>
			void queryClient.invalidateQueries({
				queryKey: ["package-community-reviews", packageName],
			}),
	});
	if (details.isPending)
		return <p className="text-muted-foreground">Loading package…</p>;
	if (details.isError) throw details.error;
	const data = details.data;
	return (
		<section>
			<div className="flex flex-wrap items-start justify-between gap-4">
				<div>
					<p className="text-sm font-medium text-primary">{data.package.category}</p>
					<h1 className="mt-1 text-3xl font-bold">{data.package.name}</h1>
					<p className="mt-3 max-w-2xl text-muted-foreground">
						{data.package.description}
					</p>
				</div>
				<div className="flex gap-2">
					{data.latestDownloadUrl && (
						<a className={buttonVariants()} href={data.latestDownloadUrl}>
							Download latest{data.latestVersion ? ` ${data.latestVersion}` : ""}
						</a>
					)}
					<Link
						to="/packages/$packageName/docs"
						params={{ packageName }}
						search={{ version: data.latestVersion ?? "" }}
						className={buttonVariants({ variant: "outline" })}
					>
						Documentation
					</Link>
				</div>
			</div>
			<div className="mt-6 flex flex-wrap gap-2">
				{data.package.tags.map((tag) => (
					<Badge key={tag} variant="secondary">
						{tag}
					</Badge>
				))}
			</div>
			<Card className="mt-6">
				<CardContent className="grid gap-3 py-5 text-sm text-muted-foreground sm:grid-cols-2">
					<span>{data.package.totalDownloads.toLocaleString()} downloads</span>
					<span>Published by {data.package.ownerDisplayName}</span>
					<span>
						Updated {new Date(data.package.updatedAtUtc).toLocaleDateString()}
					</span>
					<span>{data.dependentsCount.toLocaleString()} dependents</span>
					{data.package.repositoryUrl && (
						<a className="text-primary underline" href={data.package.repositoryUrl}>
							Source repository
						</a>
					)}
					{data.package.websiteUrl && (
						<a className="text-primary underline" href={data.package.websiteUrl}>
							Project website
						</a>
					)}
				</CardContent>
			</Card>
			<section className="mt-8 space-y-3">
				<h2 className="text-xl font-semibold">Community reviews</h2>
				<form
					className="flex flex-wrap gap-2"
					onSubmit={(event) => {
						event.preventDefault();
						const form = new FormData(event.currentTarget);
						submitReview.mutate({
							rating: Number(form.get("rating")),
							comment: String(form.get("comment") ?? "").trim(),
						});
						event.currentTarget.reset();
					}}
				>
					<select
						name="rating"
						className="rounded-md border border-input bg-transparent px-3 py-2"
						defaultValue="5"
					>
						<option value="5">5 stars</option>
						<option value="4">4 stars</option>
						<option value="3">3 stars</option>
						<option value="2">2 stars</option>
						<option value="1">1 star</option>
					</select>
					<Input
						name="comment"
						required
						className="min-w-64 flex-1"
						placeholder="Share your experience"
					/>
					<Button type="submit" disabled={submitReview.isPending}>
						{submitReview.isPending ? "Posting…" : "Post review"}
					</Button>
				</form>
				{submitReview.isError && (
					<p className="text-sm text-destructive">
						Your review could not be posted.
					</p>
				)}
				{reviews.isError ? (
					<p className="text-sm text-destructive">Reviews could not be loaded.</p>
				) : (
					<div className="space-y-2">
						{reviews.data?.map((review) => (
							<Card key={review.id}>
								<CardContent className="py-3 text-sm">
									<strong>{review.rating}/5</strong> · {review.author}
									<p className="mt-1 whitespace-pre-wrap">{review.comment}</p>
								</CardContent>
							</Card>
						))}
					</div>
				)}
			</section>
			<h2 className="mt-8 text-xl font-semibold">Versions</h2>
			<ul className="mt-3 space-y-2">
				{data.versions.length === 0 ? (
					<li className="rounded-md border border-border px-4 py-3 text-muted-foreground">
						No released versions yet.
					</li>
				) : (
					data.versions.map((version) => (
						<li
							key={version.version}
							className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-4 py-3"
						>
							<div>
								<span className="font-medium">
									{version.version}
									{version.isYanked ? " (yanked)" : ""}
								</span>
								<p className="mt-1 text-xs text-muted-foreground">
									{(version.sizeBytes / 1024).toFixed(1)} KiB · published{" "}
									{new Date(version.publishedAtUtc).toLocaleDateString()} · SHA-256{" "}
									{version.checksumSha256}
									{version.hasReadme ? " · README" : ""}
								</p>
							</div>
							{!version.isYanked && (
								<a
									className={buttonVariants({ variant: "outline", size: "sm" })}
									href={pckgApi.packageDownloadUrl(packageName, version.version)}
								>
									Download
								</a>
							)}
						</li>
					))
				)}
			</ul>
		</section>
	);
}
export const packageDocsRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/packages/$packageName/docs",
	validateSearch: (search: Record<string, unknown>) => ({
		version: typeof search.version === "string" ? search.version : "",
	}),
	component: PackageDocumentationPage,
});
function PackageDocumentationPage() {
	const { packageName } = useParams({ from: "/packages/$packageName/docs" });
	const { version } = useSearch({ from: "/packages/$packageName/docs" });
	const navigate = useNavigate();
	const [docPath, setDocPath] = useState<string | null>(null);
	const [sourcePath, setSourcePath] = useState<string | null>(null);
	const details = useQuery({
		queryKey: ["package", packageName],
		queryFn: () => pckgApi.getPackage(packageName),
	});
	const selectedVersion =
		version ||
		details.data?.latestVersion ||
		details.data?.versions.find((item) => !item.isYanked)?.version ||
		"";
	const docs = useQuery({
		queryKey: ["package-docs", packageName, selectedVersion],
		enabled: Boolean(selectedVersion),
		queryFn: () => pckgApi.listPackageDocs(packageName, selectedVersion),
	});
	const source = useQuery({
		queryKey: ["package-source", packageName, selectedVersion],
		enabled: Boolean(selectedVersion),
		queryFn: () => pckgApi.listPackageSource(packageName, selectedVersion),
	});
	const structured = useQuery({
		queryKey: ["package-structured-docs", packageName, selectedVersion],
		enabled: Boolean(selectedVersion),
		queryFn: () => pckgApi.getStructuredPackageDocs(packageName, selectedVersion),
	});
	const readme = useQuery({
		queryKey: ["package-readme", packageName, selectedVersion],
		enabled: Boolean(selectedVersion),
		retry: false,
		queryFn: () => pckgApi.getPackageReadme(packageName, selectedVersion),
	});
	const doc = useQuery({
		queryKey: ["package-doc", packageName, selectedVersion, docPath],
		enabled: Boolean(selectedVersion && docPath),
		queryFn: () => pckgApi.getPackageDoc(packageName, selectedVersion, docPath!),
	});
	const sourceFile = useQuery({
		queryKey: ["package-source-file", packageName, selectedVersion, sourcePath],
		enabled: Boolean(selectedVersion && sourcePath),
		queryFn: () =>
			pckgApi.getPackageSource(packageName, selectedVersion, sourcePath!),
	});
	if (details.isPending)
		return (
			<p className="text-muted-foreground">Loading package documentation…</p>
		);
	if (details.isError) throw details.error;
	if (!selectedVersion)
		return (
			<section>
				<h1 className="text-3xl font-bold">{packageName} documentation</h1>
				<p className="mt-3 text-muted-foreground">
					This package has no browseable release yet.
				</p>
			</section>
		);
	if (docs.isError) throw docs.error;
	if (source.isError) throw source.error;
	if (structured.isError) throw structured.error;
	const documentation = docs.data ?? [];
	const sourceEntries = source.data ?? [];
	return (
		<section className="space-y-6">
			<header className="flex flex-wrap items-end justify-between gap-4">
				<div>
					<p className="text-sm font-medium text-primary">Package artifact</p>
					<h1 className="mt-1 text-3xl font-bold">{packageName} documentation</h1>
					<p className="mt-2 text-muted-foreground">
						Read the files verified in this published release.
					</p>
				</div>
				<label className="grid gap-1 text-sm font-medium">
					Version
					<select
						className="rounded-md border border-input bg-transparent px-3 py-2"
						value={selectedVersion}
						onChange={(event) => {
							setDocPath(null);
							setSourcePath(null);
							void navigate({
								to: "/packages/$packageName/docs",
								params: { packageName },
								search: { version: event.target.value },
							});
						}}
					>
						{details.data.versions.map((item) => (
							<option key={item.version} value={item.version}>
								{item.version}
								{item.isYanked ? " (yanked)" : ""}
							</option>
						))}
					</select>
				</label>
			</header>
			{readme.data && (
				<Card>
					<CardHeader>
						<CardTitle>README</CardTitle>
					</CardHeader>
					<CardContent>
						<pre className="overflow-x-auto whitespace-pre-wrap text-sm">
							{readme.data}
						</pre>
					</CardContent>
				</Card>
			)}
			{Boolean(structured.data?.metadata) && (
				<Card>
					<CardHeader>
						<CardTitle>Package metadata</CardTitle>
					</CardHeader>
					<CardContent>
						<pre className="overflow-x-auto whitespace-pre-wrap text-sm">
							{JSON.stringify(structured.data?.metadata, null, 2)}
						</pre>
					</CardContent>
				</Card>
			)}
			<div className="grid gap-6 lg:grid-cols-2">
				<Card>
					<CardHeader>
						<CardTitle>Documentation files</CardTitle>
						<CardDescription>
							Markdown files packaged with version {selectedVersion}.
						</CardDescription>
					</CardHeader>
					<CardContent className="space-y-2">
						{documentation.length === 0 ? (
							<p className="text-sm text-muted-foreground">
								No documentation files were published.
							</p>
						) : (
							documentation.map((entry) => (
								<Button
									key={entry.path}
									variant="outline"
									className="w-full justify-between"
									onClick={() => setDocPath(entry.path)}
								>
									{entry.path}
									<span className="text-muted-foreground">{entry.sizeBytes} B</span>
								</Button>
							))
						)}
						{doc.isError && (
							<p className="text-sm text-destructive">
								Could not load this documentation file.
							</p>
						)}
						{doc.data && (
							<pre className="max-h-96 overflow-auto whitespace-pre-wrap rounded-md border border-border p-3 text-sm">
								{doc.data}
							</pre>
						)}
					</CardContent>
				</Card>
				<Card>
					<CardHeader>
						<CardTitle>Source tree</CardTitle>
						<CardDescription>
							Source files from the verified package artifact.
						</CardDescription>
					</CardHeader>
					<CardContent className="space-y-2">
						{sourceEntries.length === 0 ? (
							<p className="text-sm text-muted-foreground">
								No source files were published.
							</p>
						) : (
							sourceEntries.map((entry) => (
								<Button
									key={entry.path}
									variant="outline"
									className="w-full justify-between"
									onClick={() => setSourcePath(entry.path)}
								>
									{entry.path}
									<span className="text-muted-foreground">{entry.sizeBytes} B</span>
								</Button>
							))
						)}
						{sourceFile.isError && (
							<p className="text-sm text-destructive">
								Could not load this source file.
							</p>
						)}
						{sourceFile.data && (
							<pre className="max-h-96 overflow-auto whitespace-pre-wrap rounded-md border border-border p-3 text-sm">
								{sourceFile.data}
							</pre>
						)}
					</CardContent>
				</Card>
			</div>
			<PackageSourceGraphPanel
				sourceEntries={sourceEntries}
				selectedPath={sourcePath}
				onSelectPath={setSourcePath}
			/>
		</section>
	);
}

export const packageUploadRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/packages/upload",
	component: PackageUploadPage,
});
function PackageUploadPage() {
	const navigate = useNavigate();
	const publish = useMutation({ mutationFn: pckgApi.publishPackage });
	const submit = async (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const artifact = form.get("artifact");
		if (!(artifact instanceof File) || artifact.size === 0) return;
		try {
			await publish.mutateAsync({
				packageName: String(form.get("packageName") ?? "").trim(),
				version: String(form.get("version") ?? "").trim(),
				artifact,
			});
			await navigate({ to: "/dashboard/packages/my" });
		} catch (error) {
			if (error instanceof PckgApiError && error.status === 401)
				await navigate(toDashboardGuardDestination("/dashboard/packages/upload"));
		}
	};
	return (
		<section className="max-w-2xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Upload package</h1>
				<p className="mt-2 text-muted-foreground">
					Publish a signed `.bpk` archive to an existing package. The registry
					derives and verifies its checksum.
				</p>
			</header>
			<Card>
				<CardContent className="pt-6">
					<form className="space-y-4" onSubmit={submit}>
						<label className="grid gap-2 text-sm font-medium">
							Package name
							<Input name="packageName" required placeholder="beskid.http" />
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Version
							<Input name="version" required placeholder="1.2.3" />
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Package archive
							<Input
								name="artifact"
								type="file"
								accept=".bpk,application/zip"
								required
							/>
						</label>
						{publish.isError && (
							<p className="text-sm text-destructive">
								{publish.error instanceof PckgApiError && publish.error.status === 401
									? "Your Auth Hub session has expired. Sign in and try again."
									: "The package could not be published. Check its archive and version."}
							</p>
						)}
						<Button type="submit" disabled={publish.isPending}>
							{publish.isPending ? "Publishing…" : "Publish package"}
						</Button>
					</form>
				</CardContent>
			</Card>
		</section>
	);
}

export const myPackagesRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/packages/my",
	component: MyPackagesPage,
});
function MyPackagesPage() {
	const packages = useQuery({
		queryKey: ["packages", "owner", "me"],
		queryFn: () => pckgApi.listPackages({ owner: "me" }),
	});
	if (packages.isPending)
		return <p className="text-muted-foreground">Loading your packages…</p>;
	if (packages.isError) throw packages.error;
	return (
		<section className="space-y-6">
			<header className="flex flex-wrap items-end justify-between gap-4">
				<div>
					<h1 className="text-3xl font-bold">My packages</h1>
					<p className="mt-2 text-muted-foreground">
						Packages owned by your GitHub-backed Auth Hub subject.
					</p>
				</div>
				<Link to="/dashboard/packages/upload" className={buttonVariants()}>
					Upload package
				</Link>
			</header>
			<div className="grid gap-4 md:grid-cols-2">
				{packages.data.length === 0 ? (
					<Card className="md:col-span-2">
						<CardContent className="py-8 text-muted-foreground">
							You do not own any packages yet.
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
								{item.totalDownloads.toLocaleString()} downloads · updated{" "}
								{new Date(item.updatedAtUtc).toLocaleDateString()}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}
