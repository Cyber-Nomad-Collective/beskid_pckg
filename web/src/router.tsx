import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useState } from "react";
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

import { PackageSourceGraphPanel } from "./components/package-source-graph-panel";
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

const homeRoute = createRoute({ getParentRoute: () => rootRoute, path: "/", component: HomePage });
function HomePage() { return <section className="py-12"><p className="text-sm font-medium text-primary">Beskid registry</p><h1 className="mt-3 max-w-2xl text-5xl font-bold tracking-tight">Publish and discover Beskid packages.</h1><p className="mt-5 max-w-xl text-lg text-muted-foreground">Browse public libraries, follow package discussions, and manage your releases through a GitHub-only Beskid Auth Hub identity.</p><div className="mt-8 flex flex-wrap gap-3"><Link to="/packages" search={{ q: "" }} className={buttonVariants()}>Explore packages</Link><Link to="/topics" className={buttonVariants({ variant: "outline" })}>Visit community</Link></div></section>; }

const packagesRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages", validateSearch: (search: Record<string, unknown>) => ({ q: typeof search.q === "string" ? search.q : "" }), component: PackagesPage });
function PackagesPage() {
	const { q } = useSearch({ from: "/packages" });
	const packages = useQuery({ queryKey: ["packages", q], queryFn: () => pckgApi.listPackages({ query: q || undefined }) });
	if (packages.isPending) return <p className="text-muted-foreground">Loading packages…</p>;
	if (packages.isError) throw packages.error;
	return <section><div className="flex flex-wrap items-end justify-between gap-4"><div><h1 className="text-3xl font-bold">Packages</h1><p className="mt-2 text-muted-foreground">Find public packages for your Beskid projects.</p></div><form className="flex gap-2" action="/packages"><Input name="q" defaultValue={q} placeholder="Search packages" aria-label="Search packages" /><Button type="submit" variant="outline">Search</Button></form></div><div className="mt-6 grid gap-4 md:grid-cols-2">{packages.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-8 text-muted-foreground">No packages match this search.</CardContent></Card> : packages.data.map((item) => <Card key={item.id}><CardHeader><CardTitle><Link to="/packages/$packageName" params={{ packageName: item.name }} className="hover:underline">{item.name}</Link></CardTitle><CardDescription>{item.description}</CardDescription></CardHeader><CardContent className="flex flex-wrap gap-2 text-sm text-muted-foreground"><span>{item.category}</span><span>·</span><span>{item.totalDownloads.toLocaleString()} downloads</span><span>·</span><span>{item.ownerDisplayName}</span>{item.tags.slice(0, 3).map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</CardContent></Card>)}</div></section>;
}

const packageRoute = createRoute({ getParentRoute: () => rootRoute, path: "/packages/$packageName", component: PackageDetailsPage });
function PackageDetailsPage() {
	const { packageName } = useParams({ from: "/packages/$packageName" });
	const details = useQuery({ queryKey: ["package", packageName], queryFn: () => pckgApi.getPackage(packageName) });
	const reviews = useQuery({ queryKey: ["package-community-reviews", packageName], queryFn: () => pckgApi.listPackageCommunityReviews(packageName) });
	const queryClient = useQueryClient();
	const submitReview = useMutation({ mutationFn: (input: { rating: number; comment: string }) => pckgApi.createPackageCommunityReview(packageName, input), onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["package-community-reviews", packageName] }) });
	if (details.isPending) return <p className="text-muted-foreground">Loading package…</p>;
	if (details.isError) throw details.error;
	const data = details.data;
	return <section>
		<div className="flex flex-wrap items-start justify-between gap-4"><div><p className="text-sm font-medium text-primary">{data.package.category}</p><h1 className="mt-1 text-3xl font-bold">{data.package.name}</h1><p className="mt-3 max-w-2xl text-muted-foreground">{data.package.description}</p></div><div className="flex gap-2">{data.latestDownloadUrl && <a className={buttonVariants()} href={data.latestDownloadUrl}>Download latest{data.latestVersion ? ` ${data.latestVersion}` : ""}</a>}<Link to="/packages/$packageName/docs" params={{ packageName }} search={{ version: data.latestVersion ?? "" }} className={buttonVariants({ variant: "outline" })}>Documentation</Link></div></div>
		<div className="mt-6 flex flex-wrap gap-2">{data.package.tags.map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</div>
		<Card className="mt-6"><CardContent className="grid gap-3 py-5 text-sm text-muted-foreground sm:grid-cols-2"><span>{data.package.totalDownloads.toLocaleString()} downloads</span><span>Published by {data.package.ownerDisplayName}</span><span>Updated {new Date(data.package.updatedAtUtc).toLocaleDateString()}</span><span>{data.dependentsCount.toLocaleString()} dependents</span>{data.package.repositoryUrl && <a className="text-primary underline" href={data.package.repositoryUrl}>Source repository</a>}{data.package.websiteUrl && <a className="text-primary underline" href={data.package.websiteUrl}>Project website</a>}</CardContent></Card>
		<section className="mt-8 space-y-3"><h2 className="text-xl font-semibold">Community reviews</h2><form className="flex flex-wrap gap-2" onSubmit={(event) => { event.preventDefault(); const form = new FormData(event.currentTarget); submitReview.mutate({ rating: Number(form.get("rating")), comment: String(form.get("comment") ?? "").trim() }); event.currentTarget.reset(); }}><select name="rating" className="rounded-md border border-input bg-transparent px-3 py-2" defaultValue="5"><option value="5">5 stars</option><option value="4">4 stars</option><option value="3">3 stars</option><option value="2">2 stars</option><option value="1">1 star</option></select><Input name="comment" required className="min-w-64 flex-1" placeholder="Share your experience" /><Button type="submit" disabled={submitReview.isPending}>{submitReview.isPending ? "Posting…" : "Post review"}</Button></form>{submitReview.isError && <p className="text-sm text-destructive">Your review could not be posted.</p>}{reviews.isError ? <p className="text-sm text-destructive">Reviews could not be loaded.</p> : <div className="space-y-2">{reviews.data?.map((review) => <Card key={review.id}><CardContent className="py-3 text-sm"><strong>{review.rating}/5</strong> · {review.author}<p className="mt-1 whitespace-pre-wrap">{review.comment}</p></CardContent></Card>)}</div>}</section>
		<h2 className="mt-8 text-xl font-semibold">Versions</h2><ul className="mt-3 space-y-2">{data.versions.length === 0 ? <li className="rounded-md border border-border px-4 py-3 text-muted-foreground">No released versions yet.</li> : data.versions.map((version) => <li key={version.version} className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border px-4 py-3"><div><span className="font-medium">{version.version}{version.isYanked ? " (yanked)" : ""}</span><p className="mt-1 text-xs text-muted-foreground">{(version.sizeBytes / 1024).toFixed(1)} KiB · published {new Date(version.publishedAtUtc).toLocaleDateString()} · SHA-256 {version.checksumSha256}{version.hasReadme ? " · README" : ""}</p></div>{!version.isYanked && <a className={buttonVariants({ variant: "outline", size: "sm" })} href={pckgApi.packageDownloadUrl(packageName, version.version)}>Download</a>}</li>)}</ul>
	</section>;
}
const packageDocsRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/packages/$packageName/docs",
	validateSearch: (search: Record<string, unknown>) => ({ version: typeof search.version === "string" ? search.version : "" }),
	component: PackageDocumentationPage,
});
function PackageDocumentationPage() {
	const { packageName } = useParams({ from: "/packages/$packageName/docs" });
	const { version } = useSearch({ from: "/packages/$packageName/docs" });
	const navigate = useNavigate();
	const [docPath, setDocPath] = useState<string | null>(null);
	const [sourcePath, setSourcePath] = useState<string | null>(null);
	const details = useQuery({ queryKey: ["package", packageName], queryFn: () => pckgApi.getPackage(packageName) });
	const selectedVersion = version || details.data?.latestVersion || details.data?.versions.find((item) => !item.isYanked)?.version || "";
	const docs = useQuery({ queryKey: ["package-docs", packageName, selectedVersion], enabled: Boolean(selectedVersion), queryFn: () => pckgApi.listPackageDocs(packageName, selectedVersion) });
	const source = useQuery({ queryKey: ["package-source", packageName, selectedVersion], enabled: Boolean(selectedVersion), queryFn: () => pckgApi.listPackageSource(packageName, selectedVersion) });
	const structured = useQuery({ queryKey: ["package-structured-docs", packageName, selectedVersion], enabled: Boolean(selectedVersion), queryFn: () => pckgApi.getStructuredPackageDocs(packageName, selectedVersion) });
	const readme = useQuery({ queryKey: ["package-readme", packageName, selectedVersion], enabled: Boolean(selectedVersion), retry: false, queryFn: () => pckgApi.getPackageReadme(packageName, selectedVersion) });
	const doc = useQuery({ queryKey: ["package-doc", packageName, selectedVersion, docPath], enabled: Boolean(selectedVersion && docPath), queryFn: () => pckgApi.getPackageDoc(packageName, selectedVersion, docPath!) });
	const sourceFile = useQuery({ queryKey: ["package-source-file", packageName, selectedVersion, sourcePath], enabled: Boolean(selectedVersion && sourcePath), queryFn: () => pckgApi.getPackageSource(packageName, selectedVersion, sourcePath!) });
	if (details.isPending) return <p className="text-muted-foreground">Loading package documentation…</p>;
	if (details.isError) throw details.error;
	if (!selectedVersion) return <section><h1 className="text-3xl font-bold">{packageName} documentation</h1><p className="mt-3 text-muted-foreground">This package has no browseable release yet.</p></section>;
	if (docs.isError) throw docs.error;
	if (source.isError) throw source.error;
	if (structured.isError) throw structured.error;
	const documentation = docs.data ?? [];
	const sourceEntries = source.data ?? [];
	return <section className="space-y-6"><header className="flex flex-wrap items-end justify-between gap-4"><div><p className="text-sm font-medium text-primary">Package artifact</p><h1 className="mt-1 text-3xl font-bold">{packageName} documentation</h1><p className="mt-2 text-muted-foreground">Read the files verified in this published release.</p></div><label className="grid gap-1 text-sm font-medium">Version<select className="rounded-md border border-input bg-transparent px-3 py-2" value={selectedVersion} onChange={(event) => { setDocPath(null); setSourcePath(null); void navigate({ to: "/packages/$packageName/docs", params: { packageName }, search: { version: event.target.value } }); }}>{details.data.versions.map((item) => <option key={item.version} value={item.version}>{item.version}{item.isYanked ? " (yanked)" : ""}</option>)}</select></label></header>
		{readme.data && <Card><CardHeader><CardTitle>README</CardTitle></CardHeader><CardContent><pre className="overflow-x-auto whitespace-pre-wrap text-sm">{readme.data}</pre></CardContent></Card>}
		{Boolean(structured.data?.metadata) && <Card><CardHeader><CardTitle>Package metadata</CardTitle></CardHeader><CardContent><pre className="overflow-x-auto whitespace-pre-wrap text-sm">{JSON.stringify(structured.data!.metadata, null, 2)}</pre></CardContent></Card>}
		<div className="grid gap-6 lg:grid-cols-2"><Card><CardHeader><CardTitle>Documentation files</CardTitle><CardDescription>Markdown files packaged with version {selectedVersion}.</CardDescription></CardHeader><CardContent className="space-y-2">{documentation.length === 0 ? <p className="text-sm text-muted-foreground">No documentation files were published.</p> : documentation.map((entry) => <Button key={entry.path} variant="outline" className="w-full justify-between" onClick={() => setDocPath(entry.path)}>{entry.path}<span className="text-muted-foreground">{entry.sizeBytes} B</span></Button>)}{doc.isError && <p className="text-sm text-destructive">Could not load this documentation file.</p>}{doc.data && <pre className="max-h-96 overflow-auto whitespace-pre-wrap rounded-md border border-border p-3 text-sm">{doc.data}</pre>}</CardContent></Card><Card><CardHeader><CardTitle>Source tree</CardTitle><CardDescription>Source files from the verified package artifact.</CardDescription></CardHeader><CardContent className="space-y-2">{sourceEntries.length === 0 ? <p className="text-sm text-muted-foreground">No source files were published.</p> : sourceEntries.map((entry) => <Button key={entry.path} variant="outline" className="w-full justify-between" onClick={() => setSourcePath(entry.path)}>{entry.path}<span className="text-muted-foreground">{entry.sizeBytes} B</span></Button>)}{sourceFile.isError && <p className="text-sm text-destructive">Could not load this source file.</p>}{sourceFile.data && <pre className="max-h-96 overflow-auto whitespace-pre-wrap rounded-md border border-border p-3 text-sm">{sourceFile.data}</pre>}</CardContent></Card></div>
		<PackageSourceGraphPanel
			sourceEntries={sourceEntries}
			selectedPath={sourcePath}
			onSelectPath={setSourcePath}
		/>
	</section>;
}

const publishersRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers", component: PublishersPage });
function PublishersPage() {
	const publishers = useQuery({ queryKey: ["publishers"], queryFn: pckgApi.listPublishers });
	if (publishers.isPending) return <p className="text-muted-foreground">Loading publishers…</p>;
	if (publishers.isError) throw publishers.error;
	return <section><header><h1 className="text-3xl font-bold">Publishers</h1><p className="mt-2 text-muted-foreground">Discover public profiles linked to a GitHub-only Beskid Auth Hub subject.</p></header><div className="mt-6 grid gap-4 md:grid-cols-2">{publishers.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-8 text-muted-foreground">No public publisher profiles yet.</CardContent></Card> : publishers.data.map((publisher) => <Card key={publisher.subject}><CardHeader><CardTitle><Link to="/publishers/$publisher" params={{ publisher: publisher.subject }} className="hover:underline">{publisher.display_name}</Link></CardTitle><CardDescription>{publisher.subject}</CardDescription></CardHeader><CardContent><p className="text-sm text-muted-foreground">{publisher.bio || "No biography provided."}</p></CardContent></Card>)}</div></section>;
}
const publisherRoute = createRoute({ getParentRoute: () => rootRoute, path: "/publishers/$publisher", component: PublisherPage });
function PublisherPage() {
	const { publisher } = useParams({ from: "/publishers/$publisher" });
	const profile = useQuery({ queryKey: ["community-profile", publisher], queryFn: () => pckgApi.getCommunityProfile(publisher) });
	const packages = useQuery({ queryKey: ["publisher-packages", publisher], queryFn: () => pckgApi.listPublisherPackages(publisher) });
	const follow = useMutation({ mutationFn: () => pckgApi.togglePublisherFollow(publisher) });
	if (profile.isPending) return <p className="text-muted-foreground">Loading publisher profile…</p>;
	if (profile.isError && profile.error instanceof PckgApiError && profile.error.status === 404) return <section><h1 className="text-3xl font-bold">{publisher}</h1><p className="mt-3 text-muted-foreground">No public profile exists for this Auth Hub subject.</p></section>;
	if (profile.isError) throw profile.error;
	if (packages.isError) throw packages.error;
	return <section><div className="flex flex-wrap items-start justify-between gap-4"><div><h1 className="text-3xl font-bold">{profile.data.display_name}</h1><p className="mt-3 text-muted-foreground">{profile.data.bio || "No biography provided."}</p></div><Button variant="outline" onClick={() => follow.mutate()} disabled={follow.isPending}>{follow.isPending ? "Updating…" : follow.data?.is_following ? "Following" : "Follow publisher"}</Button></div>{follow.isError && <p className="mt-3 text-sm text-destructive">Could not update this follow.</p>}{profile.data.social_links.length > 0 && <ul className="mt-5 space-y-2">{profile.data.social_links.map((link) => <li key={link}><a className="text-primary underline" href={link}>{link}</a></li>)}</ul>}<section className="mt-8"><h2 className="text-xl font-semibold">Published packages</h2>{packages.isPending ? <p className="mt-3 text-muted-foreground">Loading published packages…</p> : <div className="mt-3 grid gap-4 md:grid-cols-2">{packages.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-6 text-muted-foreground">This publisher has no public packages yet.</CardContent></Card> : packages.data.map((item) => <Card key={item.id}><CardHeader><CardTitle><Link to="/packages/$packageName" params={{ packageName: item.name }} className="hover:underline">{item.name}</Link></CardTitle><CardDescription>{item.description}</CardDescription></CardHeader><CardContent className="flex flex-wrap gap-2 text-sm text-muted-foreground"><span>{item.category}</span><span>·</span><span>{item.totalDownloads.toLocaleString()} downloads</span>{item.tags.slice(0, 3).map((tag) => <Badge key={tag} variant="secondary">{tag}</Badge>)}</CardContent></Card>)}</div>}</section></section>;
}
const topicsRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics", component: TopicsPage });
function TopicsPage() { const boards = useQuery({ queryKey: ["community-boards"], queryFn: pckgApi.listBoards }); if (boards.isPending) return <p className="text-muted-foreground">Loading community boards…</p>; if (boards.isError) throw boards.error; return <section><header><h1 className="text-3xl font-bold">Public topics</h1><p className="mt-2 text-muted-foreground">Discuss packages and the Beskid ecosystem.</p></header><div className="mt-6 grid gap-3">{boards.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No public boards yet.</CardContent></Card> : boards.data.map((board) => <Card key={board.id}><CardHeader><CardTitle><Link to="/topics/$topic" params={{ topic: board.id }} className="hover:underline">{board.title}</Link></CardTitle><CardDescription>{board.locked ? "This board is read-only." : "Start or join a discussion."}</CardDescription></CardHeader></Card>)}</div></section>; }
const topicRoute = createRoute({ getParentRoute: () => rootRoute, path: "/topics/$topic", component: TopicPage });
function TopicPage() {
	const { topic } = useParams({ from: "/topics/$topic" }); const queryClient = useQueryClient();
	const board = useQuery({ queryKey: ["community-board", topic], queryFn: () => pckgApi.getBoard(topic) });
	const posts = useQuery({ queryKey: ["community-board-posts", topic], queryFn: () => pckgApi.listBoardPosts(topic) });
	const create = useMutation({ mutationFn: (input: { title: string; content: string }) => pckgApi.createPost(topic, input), onSuccess: () => queryClient.invalidateQueries({ queryKey: ["community-board-posts", topic] }) });
	if (board.isPending || posts.isPending) return <p className="text-muted-foreground">Loading topic…</p>; if (board.isError) throw board.error; if (posts.isError) throw posts.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => { event.preventDefault(); const form = new FormData(event.currentTarget); create.mutate({ title: String(form.get("title") ?? "").trim(), content: String(form.get("content") ?? "").trim() }); event.currentTarget.reset(); };
	return <section className="space-y-6"><header><h1 className="text-3xl font-bold">{board.data.title}</h1><p className="mt-2 text-muted-foreground">{board.data.locked ? "This board is read-only." : "New discussions are visible to the community."}</p></header>{!board.data.locked && <Card><CardContent className="pt-6"><form className="space-y-3" onSubmit={submit}><Input name="title" required placeholder="Post title" /><textarea className="min-h-28 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm" name="content" required placeholder="Start the discussion" /><Button type="submit" disabled={create.isPending}>{create.isPending ? "Posting…" : "Create post"}</Button>{create.isError && <p className="text-sm text-destructive">Could not create the post.</p>}</form></CardContent></Card>}<div className="space-y-3">{posts.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No posts yet.</CardContent></Card> : posts.data.map((post) => <Card key={post.id}><CardHeader><CardTitle><Link to="/board/post/$postId" params={{ postId: String(post.id) }} className="hover:underline">{post.title}</Link></CardTitle><CardDescription>By {post.author} · score {post.score}</CardDescription></CardHeader><CardContent className="whitespace-pre-wrap text-sm">{post.content}</CardContent></Card>)}</div></section>;
}
const boardPostRoute = createRoute({ getParentRoute: () => rootRoute, path: "/board/post/$postId", component: BoardPostPage });
function BoardPostPage() {
	const { postId } = useParams({ from: "/board/post/$postId" }); const id = Number(postId); const queryClient = useQueryClient();
	const post = useQuery({ queryKey: ["community-post", id], queryFn: () => pckgApi.getPost(id) }); const comments = useQuery({ queryKey: ["community-post-comments", id], queryFn: () => pckgApi.listPostComments(id) });
	const vote = useMutation({ mutationFn: (value: -1 | 1) => pckgApi.voteOnPost(id, value), onSuccess: () => queryClient.invalidateQueries({ queryKey: ["community-post", id] }) });
	const comment = useMutation({ mutationFn: (content: string) => pckgApi.createComment(id, { content }), onSuccess: () => queryClient.invalidateQueries({ queryKey: ["community-post-comments", id] }) });
	if (post.isPending || comments.isPending) return <p className="text-muted-foreground">Loading post…</p>; if (post.isError) throw post.error; if (comments.isError) throw comments.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => { event.preventDefault(); const content = String(new FormData(event.currentTarget).get("content") ?? "").trim(); if (content) { comment.mutate(content); event.currentTarget.reset(); } };
	return <section className="space-y-6"><Card><CardHeader><CardTitle>{post.data.title}</CardTitle><CardDescription>By {post.data.author} · score {post.data.score}</CardDescription></CardHeader><CardContent><p className="whitespace-pre-wrap">{post.data.content}</p><div className="mt-4 flex gap-2"><Button size="sm" variant="outline" onClick={() => vote.mutate(1)}>Upvote</Button><Button size="sm" variant="outline" onClick={() => vote.mutate(-1)}>Downvote</Button></div></CardContent></Card><section><h2 className="text-xl font-semibold">Comments</h2><div className="mt-3 space-y-3">{comments.data.map((item) => <Card key={item.id}><CardContent className="py-4 text-sm"><p className="font-medium">{item.author} · score {item.score}</p><p className="mt-2 whitespace-pre-wrap">{item.content}</p></CardContent></Card>)}</div><Card className="mt-4"><CardContent className="pt-6"><form className="space-y-3" onSubmit={submit}><textarea className="min-h-24 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm" name="content" required placeholder="Add a comment" /><Button type="submit" disabled={comment.isPending}>{comment.isPending ? "Posting…" : "Comment"}</Button>{comment.isError && <p className="text-sm text-destructive">Could not add the comment.</p>}</form></CardContent></Card></section></section>;
}

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
	const queryClient = useQueryClient();
	const notifications = useQuery({ queryKey: ["notifications"], queryFn: pckgApi.listNotifications });
	const preference = useMutation({ mutationFn: pckgApi.updateNotificationPreference });
	const markRead = useMutation({ mutationFn: pckgApi.markNotificationRead, onSuccess: () => queryClient.invalidateQueries({ queryKey: ["notifications"] }) });
	if (notifications.isPending) return <p className="text-muted-foreground">Loading notifications…</p>;
	if (notifications.isError) throw notifications.error;
	return <section className="space-y-6"><header><h1 className="text-3xl font-bold">Notifications</h1><p className="mt-2 text-muted-foreground">Control community notifications and mark messages read when you have handled them.</p></header><Card><CardContent className="py-5"><div className="flex flex-wrap gap-2"><Button variant="outline" disabled={preference.isPending} onClick={() => preference.mutate("all")}>All community notifications</Button><Button variant="outline" disabled={preference.isPending} onClick={() => preference.mutate("mentionsOnly")}>Mentions only</Button></div>{preference.isError && <p className="mt-3 text-sm text-destructive">Could not update notification preference.</p>}</CardContent></Card><div className="space-y-3">{notifications.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No notifications.</CardContent></Card> : notifications.data.map((notice) => <Card key={notice.id}><CardContent className="flex flex-wrap items-center justify-between gap-3 py-4 text-sm"><p><strong>{notice.actor}</strong> triggered a <strong>{notice.scope}</strong> notification{notice.post_id !== null ? ` on post ${notice.post_id}` : ""}{notice.comment_id !== null ? ` in comment ${notice.comment_id}` : ""}.</p>{!notice.is_read && <Button size="sm" variant="outline" disabled={markRead.isPending} onClick={() => markRead.mutate(notice.id)}>Mark read</Button>}</CardContent></Card>)}</div></section>;
}
const apiKeysRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/api-keys", component: ApiKeysPage });
function ApiKeysPage() {
	const queryClient = useQueryClient();
	const [createdKey, setCreatedKey] = useState<string | null>(null);
	const keys = useQuery({ queryKey: ["api-keys"], queryFn: pckgApi.listApiKeys });
	const create = useMutation({
		mutationFn: (input: { name: string; scopes: string[] }) => pckgApi.createApiKey(input),
		onSuccess: (result) => {
			setCreatedKey(result.plainTextKey);
			void queryClient.invalidateQueries({ queryKey: ["api-keys"] });
		},
	});
	const revoke = useMutation({ mutationFn: (keyId: string) => pckgApi.revokeApiKey(keyId), onSuccess: () => queryClient.invalidateQueries({ queryKey: ["api-keys"] }) });
	if (keys.isPending) return <p className="text-muted-foreground">Loading API keys…</p>;
	if (keys.isError) throw keys.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const scopes = ["read", "publish"].filter((scope) => form.get(scope) === "on");
		create.mutate({ name: String(form.get("name") ?? "").trim(), scopes });
	};
	return <section className="max-w-3xl space-y-6"><header><h1 className="text-3xl font-bold">API keys</h1><p className="mt-2 text-muted-foreground">Create narrowly scoped credentials for local tools and CI. The secret is displayed only once.</p></header>{createdKey && <Card><CardHeader><CardTitle>Copy this key now</CardTitle><CardDescription>It cannot be recovered after this message is dismissed.</CardDescription></CardHeader><CardContent className="space-y-3"><code className="block overflow-x-auto rounded-md border border-border bg-muted p-3 text-sm">{createdKey}</code><Button variant="outline" onClick={() => setCreatedKey(null)}>I copied it</Button></CardContent></Card>}<Card><CardHeader><CardTitle>Create API key</CardTitle></CardHeader><CardContent className="pt-1"><form className="space-y-4" onSubmit={submit}><label className="grid gap-2 text-sm font-medium">Name<Input name="name" required placeholder="CI publishing" /></label><fieldset className="space-y-2"><legend className="text-sm font-medium">Scopes</legend><label className="flex items-center gap-2 text-sm"><input name="read" type="checkbox" defaultChecked />Read public and permitted package data</label><label className="flex items-center gap-2 text-sm"><input name="publish" type="checkbox" defaultChecked />Publish package versions</label></fieldset>{create.isError && <p className="text-sm text-destructive">Could not create this API key. Check its name and scopes.</p>}<Button type="submit" disabled={create.isPending}>{create.isPending ? "Creating…" : "Create API key"}</Button></form></CardContent></Card><div className="space-y-3">{keys.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No API keys yet.</CardContent></Card> : keys.data.map((key) => <Card key={key.id}><CardContent className="flex flex-wrap items-center justify-between gap-3 py-4"><div><p className="font-medium">{key.name}</p><p className="mt-1 text-sm text-muted-foreground"><code>{key.prefix}</code> · {key.scopes.join(", ")} · created {new Date(key.createdAtUtc).toLocaleDateString()}{key.revokedAtUtc ? ` · revoked ${new Date(key.revokedAtUtc).toLocaleDateString()}` : ""}</p></div>{!key.revokedAtUtc && <Button size="sm" variant="outline" disabled={revoke.isPending} onClick={() => revoke.mutate(key.id)}>Revoke</Button>}</CardContent></Card>)}</div>{revoke.isError && <p className="text-sm text-destructive">Could not revoke this API key.</p>}</section>;
}
const myPackagesRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/packages/my", component: MyPackagesPage });
function MyPackagesPage() {
	const packages = useQuery({ queryKey: ["packages", "owner", "me"], queryFn: () => pckgApi.listPackages({ owner: "me" }) });
	if (packages.isPending) return <p className="text-muted-foreground">Loading your packages…</p>;
	if (packages.isError) throw packages.error;
	return <section className="space-y-6"><header className="flex flex-wrap items-end justify-between gap-4"><div><h1 className="text-3xl font-bold">My packages</h1><p className="mt-2 text-muted-foreground">Packages owned by your GitHub-backed Auth Hub subject.</p></div><Link to="/dashboard/packages/upload" className={buttonVariants()}>Upload package</Link></header><div className="grid gap-4 md:grid-cols-2">{packages.data.length === 0 ? <Card className="md:col-span-2"><CardContent className="py-8 text-muted-foreground">You do not own any packages yet.</CardContent></Card> : packages.data.map((item) => <Card key={item.id}><CardHeader><CardTitle><Link to="/packages/$packageName" params={{ packageName: item.name }} className="hover:underline">{item.name}</Link></CardTitle><CardDescription>{item.description}</CardDescription></CardHeader><CardContent className="text-sm text-muted-foreground">{item.totalDownloads.toLocaleString()} downloads · updated {new Date(item.updatedAtUtc).toLocaleDateString()}</CardContent></Card>)}</div></section>;
}

function adminErrorMessage(error: unknown): string {
	if (!(error instanceof PckgApiError)) return "The registry could not complete this administrative request.";
	if (error.status === 401) return "Your Auth Hub session has expired. Sign in again to continue.";
	if (error.status === 403) return "Your GitHub-backed account does not have permission to administer the registry.";
	if (error.status === 404) return "The requested administrative record no longer exists.";
	return "The registry could not complete this administrative request.";
}

const adminRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/admin", component: AdminOverviewPage });
function AdminOverviewPage() {
	const queryClient = useQueryClient();
	const users = useQuery({ queryKey: ["admin-users"], queryFn: pckgApi.listAdminUsers });
	const permissions = useQuery({ queryKey: ["admin-permissions"], queryFn: pckgApi.listAdminPermissions });
	const grant = useMutation({
		mutationFn: pckgApi.grantAdminPermission,
		onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["admin-permissions"] }),
	});
	if (users.isPending || permissions.isPending) return <p className="text-muted-foreground">Loading administration…</p>;
	if (users.isError) throw users.error;
	if (permissions.isError) throw permissions.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		grant.mutate({
			subject: String(form.get("subject") ?? "").trim(),
			resource: String(form.get("resource") ?? "").trim(),
			capability: String(form.get("capability") ?? "moderate"),
		});
	};
	return <section className="max-w-4xl space-y-6"><header><h1 className="text-3xl font-bold">Administration</h1><p className="mt-2 text-muted-foreground">Manage GitHub-subject registry roles, publisher verification, and narrowly scoped resource permissions.</p></header><div className="grid gap-4 sm:grid-cols-2"><Card><CardHeader><CardTitle>{users.data.length} users</CardTitle><CardDescription>Roles and publisher verification are managed by immutable GitHub subjects.</CardDescription></CardHeader><CardContent><Link to="/dashboard/admin/users" className={buttonVariants({ variant: "outline" })}>Manage users</Link></CardContent></Card><Card><CardHeader><CardTitle>{permissions.data.length} permissions</CardTitle><CardDescription>Explicit grants supplement the standard role policy for a resource.</CardDescription></CardHeader></Card></div><Card><CardHeader><CardTitle>Grant resource permission</CardTitle><CardDescription>Use a GitHub subject, such as <code>github:42</code>, and a server-recognized resource identifier.</CardDescription></CardHeader><CardContent><form className="grid gap-3 md:grid-cols-[1fr_1fr_10rem_auto]" onSubmit={submit}><Input name="subject" required pattern="github:[0-9]+" placeholder="github:42" aria-label="GitHub subject" /><Input name="resource" required placeholder="package:beskid.http" aria-label="Resource" /><select name="capability" className="h-9 rounded-md border border-input bg-transparent px-3 text-sm" aria-label="Capability"><option value="moderate">Moderate</option><option value="manage">Manage</option></select><Button type="submit" disabled={grant.isPending}>{grant.isPending ? "Granting…" : "Grant"}</Button></form>{grant.isError && <p className="mt-3 text-sm text-destructive">{adminErrorMessage(grant.error)}</p>}</CardContent></Card><section><h2 className="text-xl font-semibold">Current permissions</h2><div className="mt-3 space-y-3">{permissions.data.length === 0 ? <Card><CardContent className="py-5 text-sm text-muted-foreground">No explicit permissions have been granted.</CardContent></Card> : permissions.data.map((permission) => <Card key={`${permission.subject}:${permission.resource}:${permission.capability}`}><CardContent className="py-4 text-sm"><code>{permission.subject}</code> can <strong>{permission.capability}</strong> <code>{permission.resource}</code>.</CardContent></Card>)}</div></section></section>;
}

const adminUsersRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/admin/users", component: AdminUsersPage });
const boardModerationRoute = createRoute({ getParentRoute: () => dashboardRoute, path: "/admin/boards", component: BoardModerationPage });
function BoardModerationPage() {
	const queryClient = useQueryClient();
	const boards = useQuery({ queryKey: ["community-boards"], queryFn: pckgApi.listBoards });
	const setLocked = useMutation({ mutationFn: ({ id, locked }: { id: string; locked: boolean }) => pckgApi.setBoardLocked(id, locked), onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["community-boards"] }) });
	if (boards.isPending) return <p className="text-muted-foreground">Loading boards…</p>;
	if (boards.isError) throw boards.error;
	return <section className="max-w-3xl space-y-6"><header><h1 className="text-3xl font-bold">Board moderation</h1><p className="mt-2 text-muted-foreground">Lock a board to pause new discussions. The registry enforces your moderator or delegated board permission.</p></header><div className="space-y-3">{boards.data.map((board) => <Card key={board.id}><CardContent className="flex flex-wrap items-center justify-between gap-3 py-4"><div><p className="font-medium">{board.title}</p><p className="text-sm text-muted-foreground">{board.locked ? "Locked — members cannot post." : "Open for discussion."}</p></div><Button variant="outline" disabled={setLocked.isPending} onClick={() => setLocked.mutate({ id: board.id, locked: !board.locked })}>{board.locked ? "Unlock board" : "Lock board"}</Button></CardContent></Card>)}</div>{setLocked.isError && <p className="text-sm text-destructive">You do not have permission to change this board, or the registry could not save it.</p>}</section>;
}
function AdminUsersPage() {
	const queryClient = useQueryClient();
	const users = useQuery({ queryKey: ["admin-users"], queryFn: pckgApi.listAdminUsers });
	const update = useMutation({ mutationFn: ({ subject, roles, publisherVerified }: { subject: string; roles: string[]; publisherVerified: boolean }) => pckgApi.updateAdminUser(subject, { roles, publisherVerified }), onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["admin-users"] }) });
	if (users.isPending) return <p className="text-muted-foreground">Loading registry users…</p>;
	if (users.isError) throw users.error;
	return <section className="max-w-4xl space-y-6"><header><h1 className="text-3xl font-bold">Users and roles</h1><p className="mt-2 text-muted-foreground">Changes apply to the GitHub subject shown for each account. Email addresses and local passwords are never used.</p></header>{users.data.length === 0 ? <Card><CardContent className="py-6 text-muted-foreground">No registry users are available.</CardContent></Card> : <div className="space-y-4">{users.data.map((user) => <Card key={user.subject}><CardHeader><CardTitle>{user.githubLogin}</CardTitle><CardDescription><code>{user.subject}</code></CardDescription></CardHeader><CardContent><form className="flex flex-wrap items-end justify-between gap-4" onSubmit={(event) => { event.preventDefault(); const form = new FormData(event.currentTarget); update.mutate({ subject: user.subject, roles: ["Member", "Moderator", "SuperAdmin"].filter((role) => form.get(role) === "on"), publisherVerified: form.get("publisherVerified") === "on" }); }}><fieldset className="flex flex-wrap gap-x-4 gap-y-2"><legend className="mb-2 text-sm font-medium">Roles</legend>{["Member", "Moderator", "SuperAdmin"].map((role) => <label key={role} className="flex items-center gap-2 text-sm"><input name={role} type="checkbox" defaultChecked={user.roles.includes(role)} />{role}</label>)}<label className="flex items-center gap-2 text-sm"><input name="publisherVerified" type="checkbox" defaultChecked={user.publisherVerified} />Verified publisher</label></fieldset><Button type="submit" disabled={update.isPending}>{update.isPending ? "Saving…" : "Save changes"}</Button></form>{update.isError && <p className="mt-3 text-sm text-destructive">{adminErrorMessage(update.error)}</p>}</CardContent></Card>)}</div>}</section>;
}

export const clientRoutePaths = ["/", "/packages", "/packages/$packageName", "/packages/$packageName/docs", "/publishers", "/publishers/$publisher", "/topics", "/topics/$topic", "/board/post/$postId", "/auth", "/dashboard/profile", "/dashboard/notifications", "/dashboard/api-keys", "/dashboard/packages/my", "/dashboard/packages/upload", "/dashboard/admin", "/dashboard/admin/users", "/dashboard/admin/boards"] as const;
const routeTree = rootRoute.addChildren([homeRoute, packagesRoute, packageRoute, packageDocsRoute, publishersRoute, publisherRoute, topicsRoute, topicRoute, boardPostRoute, authRoute, dashboardRoute.addChildren([profileRoute, notificationsRoute, apiKeysRoute, myPackagesRoute, packageUploadRoute, adminRoute, adminUsersRoute, boardModerationRoute])]);
export const router = createRouter({ routeTree, context: { queryClient: undefined! }, defaultErrorComponent: ErrorPage, defaultNotFoundComponent: NotFoundPage });
declare module "@tanstack/react-router" { interface Register { router: typeof router; } }
