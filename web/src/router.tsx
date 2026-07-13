import { useMutation, useQuery, type QueryClient } from "@tanstack/react-query";
import {
	Link,
	Outlet,
	createRootRouteWithContext,
	createRoute,
	createRouter,
	redirect,
	useNavigate,
	useParams,
	useSearch,
} from "@tanstack/react-router";
import { BeskidHub } from "@beskid/beskid-ui/react/BeskidHub";
import { AuthPageShell } from "@beskid/ui-react/auth";
import { Badge } from "@beskid/ui-react/ui/badge";
import { Button, buttonVariants } from "@beskid/ui-react/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@beskid/ui-react/ui/card";
import { Input } from "@beskid/ui-react/ui/input";

import { buildAuthHubLoginUrl, toDashboardGuardDestination } from "./lib/auth-navigation";
import { PckgApiError, pckgApi } from "./lib/pckg-api";

export interface RouterContext { queryClient: QueryClient; }

function AppShell() {
	return <div className="min-h-screen bg-background text-foreground">
		<header className="border-b border-border"><nav className="mx-auto flex max-w-6xl items-center gap-5 px-5 py-3" aria-label="Primary navigation">
			<BeskidHub /><Link to="/" className="font-semibold tracking-tight">pckg</Link>
			<Link to="/packages" search={{ q: "" }} className="text-sm text-muted-foreground hover:text-foreground">Packages</Link>
			<Link to="/topics" className="text-sm text-muted-foreground hover:text-foreground">Community</Link>
			<Link to="/publishers" className="text-sm text-muted-foreground hover:text-foreground">Publishers</Link>
			<div className="ml-auto flex gap-2"><Link to="/auth" search={{ next: "/dashboard/packages/my" }} className={buttonVariants({ variant: "outline" })}>Sign in</Link><Link to="/dashboard/packages/my" className={buttonVariants()}>Dashboard</Link></div>
		</nav></header><main className="mx-auto max-w-6xl px-5 py-10"><Outlet /></main>
	</div>;
}

function ErrorPage({ error }: { error: unknown }) { return <Card className="mx-auto max-w-xl"><CardHeader><CardTitle>Something went wrong</CardTitle><CardDescription>{error instanceof Error ? error.message : "Please try again."}</CardDescription></CardHeader><CardContent><Link to="/" className={buttonVariants()}>Return home</Link></CardContent></Card>; }
function NotFoundPage() { return <Card className="mx-auto max-w-xl"><CardHeader><CardTitle>Page not found</CardTitle><CardDescription>The registry page you requested does not exist.</CardDescription></CardHeader><CardContent><Link to="/packages" search={{ q: "" }} className={buttonVariants()}>Browse packages</Link></CardContent></Card>; }

const rootRoute = createRootRouteWithContext<RouterContext>()({ component: AppShell, errorComponent: ErrorPage, notFoundComponent: NotFoundPage });

function PlaceholderPage({ title, description, children }: { title: string; description: string; children?: React.ReactNode }) {
	return <section className="space-y-6"><header><h1 className="text-3xl font-bold tracking-tight">{title}</h1><p className="mt-2 max-w-2xl text-muted-foreground">{description}</p></header>{children ?? <Card><CardContent className="py-6 text-sm text-muted-foreground">This interface is ready for the corresponding registry endpoint. Data management will become available as the Rust service reaches feature parity.</CardContent></Card>}</section>;
}

const homeRoute = createRoute({ getParentRoute: () => rootRoute, path: "/", component: HomePage });
function HomePage() { return <section className="py-12"><p className="text-sm font-medium text-primary">Beskid registry</p><h1 className="mt-3 max-w-2xl text-5xl font-bold tracking-tight">Publish and discover Beskid packages.</h1><p className="mt-5 max-w-xl text-lg text-muted-foreground">Browse public libraries, follow package discussions, and manage your releases through a GitHub-only Beskid Auth Hub identity.</p><div className="mt-8 flex flex-wrap gap-3"><Link to="/packages" search={{ q: "" }} className={buttonVariants()}>Explore packages</Link><Link to="/topics" className={buttonVariants({ variant: "outline" })}>Visit community</Link></div></section>; }

const packagesRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages", validateSearch: (search: Record<string, unknown>) => ({ q: typeof search.q === "string" ? search.q : "" }), component: PackagesPage });
function PackagesPage() {
	const { q } = useSearch({ from: "/packages" });
	const packages = useQuery({ queryKey: ["packages", q], queryFn: () => pckgApi.searchPackages({ query: q || undefined }) });
	if (packages.isPending) return <p className="text-muted-foreground">Loading packages…</p>;
	if (packages.isError) throw packages.error;
	return <section><div className="flex flex-wrap items-end justify-between gap-4"><div><h1 className="text-3xl font-bold">Packages</h1><p className="mt-2 text-muted-foreground">Find public packages for your Beskid projects.</p></div><form className="flex gap-2" action="/packages"><Input name="q" defaultValue={q} placeholder="Search packages" aria-label="Search packages" /><Button type="submit" variant="outline">Search</Button></form></div><div className="mt-6 grid gap-4 md:grid-cols-2">{packages.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-8 text-muted-foreground">No packages match this search.</CardContent></Card> : packages.data.map(({ package: item }) => <Card key={item.id}><CardHeader><CardTitle><Link to="/packages/$packageName" params={{ packageName: item.name }} className="hover:underline">{item.name}</Link></CardTitle><CardDescription>{item.description}</CardDescription></CardHeader><CardContent className="flex flex-wrap gap-2 text-sm text-muted-foreground"><span>{item.totalDownloads.toLocaleString()} downloads</span><span>·</span><span>{item.ownerDisplayName}</span>{item.tags.slice(0, 3).map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</CardContent></Card>)}</div></section>;
}

const packageRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages/$packageName", component: PackageDetailsPage });
function PackageDetailsPage() { const { packageName } = useParams({ from: "/packages/$packageName" }); const details = useQuery({ queryKey: ["package", packageName], queryFn: () => pckgApi.getPackage(packageName) }); if (details.isPending) return <p className="text-muted-foreground">Loading package…</p>; if (details.isError) throw details.error; const data = details.data; return <section><div className="flex flex-wrap items-start justify-between gap-4"><div><h1 className="text-3xl font-bold">{data.package.name}</h1><p className="mt-3 max-w-2xl text-muted-foreground">{data.package.description}</p></div><Link to="/packages/$packageName/docs" params={{ packageName }} className={buttonVariants({ variant: "outline" })}>Documentation</Link></div><div className="mt-6 flex flex-wrap gap-2">{data.package.tags.map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</div><h2 className="mt-8 text-xl font-semibold">Versions</h2><ul className="mt-3 space-y-2">{data.versions.map((version) => <li key={version.version} className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-4 py-3"><span>{version.version}{version.isYanked ? " (yanked)" : ""}</span>{!version.isYanked && <a className={buttonVariants({ variant: "outline", size: "sm" })} href={pckgApi.packageDownloadUrl(packageName, version.version)}>Download</a>}</li>)}</ul></section>; }
const packageDocsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages/$packageName/docs", component: () => { const { packageName } = useParams({ from: "/packages/$packageName/docs" }); return <PlaceholderPage title={`${packageName} documentation`} description="API documentation, symbols, source browsing, and package README rendering will be supplied by the registry documentation API." />; } });

const publishersRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers", component: () => <PlaceholderPage title="Publishers" description="Explore verified package publishers and their public registries." /> });
const publisherRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers/$publisher", component: () => { const { publisher } = useParams({ from: "/publishers/$publisher" }); return <PlaceholderPage title={publisher} description="Publisher profile, package catalog, and social links." />; } });
const topicsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics", component: () => <PlaceholderPage title="Public topics" description="Community spaces for package and ecosystem discussions." /> });
const topicRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics/$topic", component: () => { const { topic } = useParams({ from: "/topics/$topic" }); return <PlaceholderPage title={topic} description="Topic posts, comments, moderation, and subscription controls." />; } });
const boardPostRoute = createRoute({ getParentRoute: () => rootRoute, path: "/board/post/$postId", component: () => { const { postId } = useParams({ from: "/board/post/$postId" }); return <PlaceholderPage title={`Post ${postId}`} description="Community post details, comments, reactions, and moderation actions." />; } });

const authRoute = createRoute({ getParentRoute: () => rootRoute, path: "/auth", validateSearch: (search: Record<string, unknown>) => ({ next: typeof search.next === "string" ? search.next : "/dashboard/packages/my" }), component: AuthPage });
function AuthPage() { const { next } = useSearch({ from: "/auth" }); const authHubUrl = import.meta.env.VITE_AUTH_HUB_PUBLIC_URL; const startSignIn = () => { if (authHubUrl) window.location.assign(buildAuthHubLoginUrl(authHubUrl)); }; return <AuthPageShell title="Sign in to pckg" description="Continue with GitHub through Beskid Auth Hub to manage packages."><Button onClick={startSignIn} disabled={!authHubUrl}>Continue with GitHub</Button><p className="mt-4 text-sm text-muted-foreground">You will return to {next} after authentication.</p>{!authHubUrl && <p className="mt-3 text-sm text-destructive">Auth Hub is not configured.</p>}</AuthPageShell>; }

const dashboardRoute = createRoute({ getParentRoute: () => rootRoute, path: "/dashboard", beforeLoad: async ({ location }) => { if (await pckgApi.getSession()) return; throw redirect(toDashboardGuardDestination(location.pathname)); }, component: DashboardLayout });
function DashboardLayout() { return <div className="grid gap-8 lg:grid-cols-[13rem_1fr]"><aside className="space-y-1 rounded-lg border border-border p-3"><p className="px-2 pb-2 text-sm font-semibold">Dashboard</p>{[["/dashboard/profile", "Profile"], ["/dashboard/api-keys", "API keys"], ["/dashboard/packages/my", "My packages"], ["/dashboard/packages/upload", "Upload package"], ["/dashboard/admin", "Administration"]].map(([to, label]) => <Link key={to} to={to} className="block rounded px-2 py-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground">{label}</Link>)}</aside><section><Outlet /></section></div>; }
const packageUploadRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/packages/upload", component: PackageUploadPage });
function PackageUploadPage() {
	const navigate = useNavigate();
	const publish = useMutation({ mutationFn: pckgApi.publishPackage });
	const submit = async (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const artifact = form.get("artifact");
		if (!(artifact instanceof File) || artifact.size === 0) return;
		try {
			await publish.mutateAsync({ packageName: String(form.get("packageName") ?? "").trim(), version: String(form.get("version") ?? "").trim(), artifact });
			await navigate({ to: "/dashboard/packages/my" });
		} catch (error) {
			if (error instanceof PckgApiError && error.status === 401) await navigate(toDashboardGuardDestination("/dashboard/packages/upload"));
		}
	};
	return <section className="max-w-2xl space-y-6"><header><h1 className="text-3xl font-bold">Upload package</h1><p className="mt-2 text-muted-foreground">Publish a signed `.bpk` archive to an existing package. The registry derives and verifies its checksum.</p></header><Card><CardContent className="pt-6"><form className="space-y-4" onSubmit={submit}><label className="grid gap-2 text-sm font-medium">Package name<Input name="packageName" required placeholder="beskid.http" /></label><label className="grid gap-2 text-sm font-medium">Version<Input name="version" required placeholder="1.2.3" /></label><label className="grid gap-2 text-sm font-medium">Package archive<Input name="artifact" type="file" accept=".bpk,application/zip" required /></label>{publish.isError && <p className="text-sm text-destructive">{publish.error instanceof PckgApiError && publish.error.status === 401 ? "Your Auth Hub session has expired. Sign in and try again." : "The package could not be published. Check its archive and version."}</p>}<Button type="submit" disabled={publish.isPending}>{publish.isPending ? "Publishing…" : "Publish package"}</Button></form></CardContent></Card></section>;
}
const dashboardPages = [
	["/profile", "Profile settings", "Manage the public profile associated with your GitHub-backed Auth Hub identity."],
	["/api-keys", "API keys", "Create and revoke scoped keys for package publishing automation."],
	["/packages/my", "My packages", "Review your packages, versions, metadata, visibility, and release status."],
	["/packages/all", "All packages", "Registry-wide package management for administrators."],
	["/admin", "Administration", "Registry overview, users, policies, and operational status."],
	["/admin/users", "Users and roles", "Manage registry roles and publisher verification."],
	["/admin/email", "Email settings", "Configure transactional email delivery for registry notifications."],
	["/admin/registry-activity", "Registry activity", "Inspect publish, moderation, and administration events."],
	["/admin/blocked-links", "Blocked links", "Maintain link safety patterns used by community and package content."],
] as const;
const dashboardChildRoutes = dashboardPages.map(([path, title, description]) => createRoute({ getParentRoute: () => dashboardRoute, path, component: () => <PlaceholderPage title={title} description={description} /> }));

export const clientRoutePaths = ["/", "/packages", "/packages/$packageName", "/packages/$packageName/docs", "/publishers", "/publishers/$publisher", "/topics", "/topics/$topic", "/board/post/$postId", "/auth", "/dashboard/profile", "/dashboard/api-keys", "/dashboard/packages/my", "/dashboard/packages/upload", "/dashboard/packages/all", "/dashboard/admin", "/dashboard/admin/users", "/dashboard/admin/email", "/dashboard/admin/registry-activity", "/dashboard/admin/blocked-links"] as const;
const routeTree = rootRoute.addChildren([homeRoute, packagesRoute, packageRoute, packageDocsRoute, publishersRoute, publisherRoute, topicsRoute, topicRoute, boardPostRoute, authRoute, dashboardRoute.addChildren([packageUploadRoute, ...dashboardChildRoutes])]);
export const router = createRouter({ routeTree, context: { queryClient: undefined! }, defaultErrorComponent: ErrorPage, defaultNotFoundComponent: NotFoundPage });
declare module "@tanstack/react-router" { interface Register { router: typeof router; } }
