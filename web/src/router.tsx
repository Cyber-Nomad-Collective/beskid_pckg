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

function UnsupportedPage({ title, description, missing }: { title: string; description: string; missing: string }) {
	return <section className="space-y-6"><header><h1 className="text-3xl font-bold tracking-tight">{title}</h1><p className="mt-2 max-w-2xl text-muted-foreground">{description}</p></header><Card><CardContent className="py-6 text-sm text-muted-foreground"><p>This area is not available yet.</p><p className="mt-2">Missing server contract: {missing}</p></CardContent></Card></section>;
}

const homeRoute = createRoute({ getParentRoute: () => rootRoute, path: "/", component: HomePage });
function HomePage() { return <section className="py-12"><p className="text-sm font-medium text-primary">Beskid registry</p><h1 className="mt-3 max-w-2xl text-5xl font-bold tracking-tight">Publish and discover Beskid packages.</h1><p className="mt-5 max-w-xl text-lg text-muted-foreground">Browse public libraries, follow package discussions, and manage your releases through a GitHub-only Beskid Auth Hub identity.</p><div className="mt-8 flex flex-wrap gap-3"><Link to="/packages" search={{ q: "" }} className={buttonVariants()}>Explore packages</Link><Link to="/topics" className={buttonVariants({ variant: "outline" })}>Visit community</Link></div></section>; }

const packagesRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages", validateSearch: (search: Record<string, unknown>) => ({ q: typeof search.q === "string" ? search.q : "" }), component: PackagesPage });
function PackagesPage() {
	const { q } = useSearch({ from: "/packages" });
	const packages = useQuery({ queryKey: ["packages", q], queryFn: () => pckgApi.listPackages({ query: q || undefined }) });
	if (packages.isPending) return <p className="text-muted-foreground">Loading packages…</p>;
	if (packages.isError) throw packages.error;
	return <section><div className="flex flex-wrap items-end justify-between gap-4"><div><h1 className="text-3xl font-bold">Packages</h1><p className="mt-2 text-muted-foreground">Find public packages for your Beskid projects.</p></div><form className="flex gap-2" action="/packages"><Input name="q" defaultValue={q} placeholder="Filter loaded packages" aria-label="Filter packages" /><Button type="submit" variant="outline">Filter</Button></form></div><div className="mt-6 grid gap-4 md:grid-cols-2">{packages.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-8 text-muted-foreground">No packages match this filter. Server-side search and paging are not available yet.</CardContent></Card> : packages.data.map((item) => <Card key={item.id}><CardHeader><CardTitle><Link to="/packages/$packageName" params={{ packageName: item.name }} className="hover:underline">{item.name}</Link></CardTitle><CardDescription>{item.description}</CardDescription></CardHeader><CardContent className="flex flex-wrap gap-2 text-sm text-muted-foreground"><span>{item.totalDownloads.toLocaleString()} downloads</span><span>·</span><span>{item.ownerDisplayName}</span>{item.tags.slice(0, 3).map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</CardContent></Card>)}</div></section>;
}

const packageRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages/$packageName", component: PackageDetailsPage });
function PackageDetailsPage() { const { packageName } = useParams({ from: "/packages/$packageName" }); const details = useQuery({ queryKey: ["package", packageName], queryFn: () => pckgApi.getPackage(packageName) }); if (details.isPending) return <p className="text-muted-foreground">Loading package…</p>; if (details.isError) throw details.error; const data = details.data; return <section><div className="flex flex-wrap items-start justify-between gap-4"><div><h1 className="text-3xl font-bold">{data.package.name}</h1><p className="mt-3 max-w-2xl text-muted-foreground">{data.package.description}</p></div><Link to="/packages/$packageName/docs" params={{ packageName }} className={buttonVariants({ variant: "outline" })}>Documentation</Link></div><div className="mt-6 flex flex-wrap gap-2">{data.package.tags.map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</div><h2 className="mt-8 text-xl font-semibold">Versions</h2><ul className="mt-3 space-y-2">{data.versions.map((version) => <li key={version.version} className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-4 py-3"><span>{version.version}{version.isYanked ? " (yanked)" : ""}</span>{!version.isYanked && <a className={buttonVariants({ variant: "outline", size: "sm" })} href={pckgApi.packageDownloadUrl(packageName, version.version)}>Download</a>}</li>)}</ul></section>; }
const packageDocsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages/$packageName/docs", component: () => { const { packageName } = useParams({ from: "/packages/$packageName/docs" }); return <UnsupportedPage title={`${packageName} documentation`} description="Documentation is not inferred from package data." missing="package documentation and source API" />; } });

const publishersRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers", component: () => <UnsupportedPage title="Publishers" description="Publisher discovery cannot be assembled from individual profiles." missing="publisher directory and package catalog API" /> });
const publisherRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers/$publisher", component: PublisherPage });
function PublisherPage() { const { publisher } = useParams({ from: "/publishers/$publisher" }); const profile = useQuery({ queryKey: ["community-profile", publisher], queryFn: () => pckgApi.getCommunityProfile(publisher) }); if (profile.isPending) return <p className="text-muted-foreground">Loading publisher profile…</p>; if (profile.isError && profile.error instanceof PckgApiError && profile.error.status === 404) return <UnsupportedPage title={publisher} description="No public profile exists for this Auth Hub subject." missing="publisher package-catalog API" />; if (profile.isError) throw profile.error; return <section><h1 className="text-3xl font-bold">{profile.data.display_name}</h1><p className="mt-3 text-muted-foreground">{profile.data.bio || "No biography provided."}</p>{profile.data.social_links.length > 0 && <ul className="mt-5 space-y-2">{profile.data.social_links.map((link) => <li key={link}><a className="text-primary underline" href={link}>{link}</a></li>)}</ul>}<p className="mt-8 text-sm text-muted-foreground">Package catalog is unavailable until the publisher catalog API is implemented.</p></section>; }
const topicsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics", component: () => <UnsupportedPage title="Public topics" description="The service can accept community writes but does not expose a board listing." missing="community board and post listing API" /> });
const topicRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics/$topic", component: () => { const { topic } = useParams({ from: "/topics/$topic" }); return <UnsupportedPage title={topic} description="Topic content is not rendered without a read API." missing="community topic and post listing API" />; } });
const boardPostRoute = createRoute({ getParentRoute: () => rootRoute, path: "/board/post/$postId", component: () => { const { postId } = useParams({ from: "/board/post/$postId" }); return <UnsupportedPage title={`Post ${postId}`} description="Post data is not exposed by the Rust service yet." missing="community post detail and comments read API" />; } });

const authRoute = createRoute({ getParentRoute: () => rootRoute, path: "/auth", validateSearch: (search: Record<string, unknown>) => ({ next: typeof search.next === "string" ? search.next : "/dashboard/packages/my" }), component: AuthPage });
function AuthPage() { const { next } = useSearch({ from: "/auth" }); const authHubUrl = import.meta.env.VITE_AUTH_HUB_PUBLIC_URL; const startSignIn = () => { if (authHubUrl) window.location.assign(buildAuthHubLoginUrl(authHubUrl)); }; return <AuthPageShell title="Sign in to pckg" description="Continue with GitHub through Beskid Auth Hub to manage packages."><Button onClick={startSignIn} disabled={!authHubUrl}>Continue with GitHub</Button><p className="mt-4 text-sm text-muted-foreground">You will return to {next} after authentication.</p>{!authHubUrl && <p className="mt-3 text-sm text-destructive">Auth Hub is not configured.</p>}</AuthPageShell>; }

const dashboardRoute = createRoute({ getParentRoute: () => rootRoute, path: "/dashboard", beforeLoad: async ({ location }) => { if (await pckgApi.getSession()) return; throw redirect(toDashboardGuardDestination(location.pathname)); }, component: DashboardLayout });
function DashboardLayout() { return <div className="grid gap-8 lg:grid-cols-[13rem_1fr]"><aside className="space-y-1 rounded-lg border border-border p-3"><p className="px-2 pb-2 text-sm font-semibold">Dashboard</p>{[["/dashboard/profile", "Profile"], ["/dashboard/notifications", "Notifications"], ["/dashboard/api-keys", "API keys"], ["/dashboard/packages/my", "My packages"], ["/dashboard/packages/upload", "Upload package"], ["/dashboard/admin", "Administration"]].map(([to, label]) => <Link key={to} to={to} className="block rounded px-2 py-1.5 text-sm text-muted-foreground hover:bg-muted hover:text-foreground">{label}</Link>)}</aside><section><Outlet /></section></div>; }
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
const profileRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/profile", component: ProfilePage });
function ProfilePage() {
	const session = useQuery({ queryKey: ["session"], queryFn: pckgApi.getSession });
	const profile = useQuery({ queryKey: ["community-profile", "me"], enabled: Boolean(session.data), queryFn: () => pckgApi.getCommunityProfile(session.data!.subject), retry: false });
	const update = useMutation({ mutationFn: pckgApi.updateMyCommunityProfile });
	if (session.isPending || profile.isPending) return <p className="text-muted-foreground">Loading profile…</p>;
	if (session.isError) throw session.error;
	const initial = profile.data;
	const submit = async (event: React.FormEvent<HTMLFormElement>) => { event.preventDefault(); const form = new FormData(event.currentTarget); await update.mutateAsync({ display_name: String(form.get("displayName") ?? "").trim(), bio: String(form.get("bio") ?? ""), social_links: String(form.get("socialLinks") ?? "").split("\n").map((link) => link.trim()).filter(Boolean) }); };
	return <section className="max-w-2xl space-y-6"><header><h1 className="text-3xl font-bold">Profile settings</h1><p className="mt-2 text-muted-foreground">Signed in as {session.data?.githubLogin}. This profile is keyed by your GitHub-backed Auth Hub subject.</p></header><Card><CardContent className="pt-6"><form className="space-y-4" onSubmit={submit}><label className="grid gap-2 text-sm font-medium">Display name<Input name="displayName" required defaultValue={initial?.display_name ?? session.data?.githubLogin ?? ""} /></label><label className="grid gap-2 text-sm font-medium">Biography<textarea className="min-h-24 rounded-md border border-input bg-transparent px-3 py-2 text-sm" name="bio" defaultValue={initial?.bio ?? ""} /></label><label className="grid gap-2 text-sm font-medium">Social links <span className="font-normal text-muted-foreground">(one URL per line)</span><textarea className="min-h-24 rounded-md border border-input bg-transparent px-3 py-2 text-sm" name="socialLinks" defaultValue={initial?.social_links.join("\n") ?? ""} /></label>{profile.isError && !(profile.error instanceof PckgApiError && profile.error.status === 404) && <p className="text-sm text-destructive">Could not load the existing profile.</p>}{update.isError && <p className="text-sm text-destructive">Could not save the profile.</p>}<Button type="submit" disabled={update.isPending}>{update.isPending ? "Saving…" : "Save profile"}</Button></form></CardContent></Card></section>;
}

const notificationsRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/notifications", component: NotificationsPage });
function NotificationsPage() {
	const notifications = useQuery({ queryKey: ["notifications"], queryFn: pckgApi.listNotifications });
	const preference = useMutation({ mutationFn: pckgApi.updateNotificationPreference });
	if (notifications.isPending) return <p className="text-muted-foreground">Loading notifications…</p>;
	if (notifications.isError) throw notifications.error;
	return <section className="space-y-6"><header><h1 className="text-3xl font-bold">Notifications</h1><p className="mt-2 text-muted-foreground">Community notifications currently remain in the registry session and are not marked read.</p></header><Card><CardContent className="py-5"><div className="flex flex-wrap gap-2"><Button variant="outline" disabled={preference.isPending} onClick={() => preference.mutate("all")}>All community notifications</Button><Button variant="outline" disabled={preference.isPending} onClick={() => preference.mutate("mentionsOnly")}>Mentions only</Button></div>{preference.isError && <p className="mt-3 text-sm text-destructive">Could not update notification preference.</p>}</CardContent></Card><div className="space-y-3">{notifications.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No notifications.</CardContent></Card> : notifications.data.map((notice, index) => <Card key={`${notice.actor}-${notice.scope}-${index}`}><CardContent className="py-4 text-sm"><strong>{notice.actor}</strong> triggered a <strong>{notice.scope}</strong> notification{notice.post_id !== null ? ` on post ${notice.post_id}` : ""}{notice.comment_id !== null ? ` in comment ${notice.comment_id}` : ""}.</CardContent></Card>)}</div></section>;
}
const dashboardPages = [
	["/api-keys", "API keys", "API-key management is not available without an authenticated key-management contract.", "API key list/create/revoke API"],
	["/packages/my", "My packages", "The package list cannot be safely filtered to the current owner yet.", "owner-scoped package listing API"],
	["/packages/all", "All packages", "Registry-wide administration is not exposed by the service.", "administrator package listing API"],
	["/admin", "Administration", "Administration remains disabled until server authorization and operations routes are available.", "administrator overview API"],
	["/admin/users", "Users and roles", "User and role changes require the operations HTTP API.", "administrator users and roles API"],
	["/admin/email", "Email settings", "Email configuration is not exposed through the registry service.", "email configuration API"],
	["/admin/registry-activity", "Registry activity", "Operational events are not exposed through the registry service.", "registry activity API"],
	["/admin/blocked-links", "Blocked links", "Blocked-link policies are not exposed through the registry service.", "blocked-link policy API"],
] as const;
const dashboardChildRoutes = dashboardPages.map(([path, title, description, missing]) => createRoute({ getParentRoute: () => dashboardRoute, path, component: () => <UnsupportedPage title={title} description={description} missing={missing} /> }));

export const clientRoutePaths = ["/", "/packages", "/packages/$packageName", "/packages/$packageName/docs", "/publishers", "/publishers/$publisher", "/topics", "/topics/$topic", "/board/post/$postId", "/auth", "/dashboard/profile", "/dashboard/notifications", "/dashboard/api-keys", "/dashboard/packages/my", "/dashboard/packages/upload", "/dashboard/packages/all", "/dashboard/admin", "/dashboard/admin/users", "/dashboard/admin/email", "/dashboard/admin/registry-activity", "/dashboard/admin/blocked-links"] as const;
const routeTree = rootRoute.addChildren([homeRoute, packagesRoute, packageRoute, packageDocsRoute, publishersRoute, publisherRoute, topicsRoute, topicRoute, boardPostRoute, authRoute, dashboardRoute.addChildren([profileRoute, notificationsRoute, packageUploadRoute, ...dashboardChildRoutes])]);
export const router = createRouter({ routeTree, context: { queryClient: undefined! }, defaultErrorComponent: ErrorPage, defaultNotFoundComponent: NotFoundPage });
declare module "@tanstack/react-router" { interface Register { router: typeof router; } }
