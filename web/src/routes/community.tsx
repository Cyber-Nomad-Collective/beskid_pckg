import { Badge } from "@beskid/ui-react/ui/badge";
import { Button } from "@beskid/ui-react/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import { Input } from "@beskid/ui-react/ui/input";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createRoute, Link, useParams } from "@tanstack/react-router";

import { PckgApiError, pckgApi } from "../lib/pckg-api";
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
					Discover public profiles linked to a GitHub-only Beskid Auth Hub subject.
				</p>
			</header>
			<div className="mt-6 grid gap-4 md:grid-cols-2">
				{publishers.data.length === 0 ? (
					<Card className="md:col-span-2">
						<CardContent className="py-8 text-muted-foreground">
							No public publisher profiles yet.
						</CardContent>
					</Card>
				) : (
					publishers.data.map((publisher) => (
						<Card key={publisher.subject}>
							<CardHeader>
								<CardTitle>
									<Link
										to="/publishers/$publisher"
										params={{ publisher: publisher.subject }}
										className="hover:underline"
									>
										{publisher.display_name}
									</Link>
								</CardTitle>
								<CardDescription>{publisher.subject}</CardDescription>
							</CardHeader>
							<CardContent>
								<p className="text-sm text-muted-foreground">
									{publisher.bio || "No biography provided."}
								</p>
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
	const profile = useQuery({
		queryKey: ["community-profile", publisher],
		queryFn: () => pckgApi.getCommunityProfile(publisher),
	});
	const packages = useQuery({
		queryKey: ["publisher-packages", publisher],
		queryFn: () => pckgApi.listPublisherPackages(publisher),
	});
	const follow = useMutation({
		mutationFn: () => pckgApi.togglePublisherFollow(publisher),
	});
	if (profile.isPending)
		return <p className="text-muted-foreground">Loading publisher profile…</p>;
	if (
		profile.isError &&
		profile.error instanceof PckgApiError &&
		profile.error.status === 404
	)
		return (
			<section>
				<h1 className="text-3xl font-bold">{publisher}</h1>
				<p className="mt-3 text-muted-foreground">
					No public profile exists for this Auth Hub subject.
				</p>
			</section>
		);
	if (profile.isError) throw profile.error;
	if (packages.isError) throw packages.error;
	return (
		<section>
			<div className="flex flex-wrap items-start justify-between gap-4">
				<div>
					<h1 className="text-3xl font-bold">{profile.data.display_name}</h1>
					<p className="mt-3 text-muted-foreground">
						{profile.data.bio || "No biography provided."}
					</p>
				</div>
				<Button
					variant="outline"
					onClick={() => follow.mutate()}
					disabled={follow.isPending}
				>
					{follow.isPending
						? "Updating…"
						: follow.data?.is_following
							? "Following"
							: "Follow publisher"}
				</Button>
			</div>
			{follow.isError && (
				<p className="mt-3 text-sm text-destructive">
					Could not update this follow.
				</p>
			)}
			{profile.data.social_links.length > 0 && (
				<ul className="mt-5 space-y-2">
					{profile.data.social_links.map((link) => (
						<li key={link}>
							<a className="text-primary underline" href={link}>
								{link}
							</a>
						</li>
					))}
				</ul>
			)}
			<section className="mt-8">
				<h2 className="text-xl font-semibold">Published packages</h2>
				{packages.isPending ? (
					<p className="mt-3 text-muted-foreground">Loading published packages…</p>
				) : (
					<div className="mt-3 grid gap-4 md:grid-cols-2">
						{packages.data.length === 0 ? (
							<Card className="md:col-span-2">
								<CardContent className="py-6 text-muted-foreground">
									This publisher has no public packages yet.
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
				)}
			</section>
		</section>
	);
}
export const topicsRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/topics",
	component: TopicsPage,
});
function TopicsPage() {
	const boards = useQuery({
		queryKey: ["community-boards"],
		queryFn: () => pckgApi.listBoards(),
	});
	if (boards.isPending)
		return <p className="text-muted-foreground">Loading community boards…</p>;
	if (boards.isError) throw boards.error;
	return (
		<section>
			<header>
				<h1 className="text-3xl font-bold">Public topics</h1>
				<p className="mt-2 text-muted-foreground">
					Discuss packages and the Beskid ecosystem.
				</p>
			</header>
			<div className="mt-6 grid gap-3">
				{boards.data.length === 0 ? (
					<Card>
						<CardContent className="py-6 text-muted-foreground">
							No public boards yet.
						</CardContent>
					</Card>
				) : (
					boards.data.map((board) => (
						<Card key={board.id}>
							<CardHeader>
								<CardTitle>
									<Link
										to="/topics/$topic"
										params={{ topic: board.id }}
										className="hover:underline"
									>
										{board.title}
									</Link>
								</CardTitle>
								<CardDescription>
									{board.locked
										? "This board is read-only."
										: "Start or join a discussion."}
								</CardDescription>
							</CardHeader>
						</Card>
					))
				)}
			</div>
		</section>
	);
}
export const topicRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/topics/$topic",
	component: TopicPage,
});
function TopicPage() {
	const { topic } = useParams({ from: "/topics/$topic" });
	const queryClient = useQueryClient();
	const board = useQuery({
		queryKey: ["community-board", topic],
		queryFn: () => pckgApi.getBoard(topic),
	});
	const posts = useQuery({
		queryKey: ["community-board-posts", topic],
		queryFn: () => pckgApi.listBoardPosts(topic),
	});
	const create = useMutation({
		mutationFn: (input: { title: string; content: string }) =>
			pckgApi.createPost(topic, input),
		onSuccess: () =>
			queryClient.invalidateQueries({
				queryKey: ["community-board-posts", topic],
			}),
	});
	if (board.isPending || posts.isPending)
		return <p className="text-muted-foreground">Loading topic…</p>;
	if (board.isError) throw board.error;
	if (posts.isError) throw posts.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		create.mutate({
			title: String(form.get("title") ?? "").trim(),
			content: String(form.get("content") ?? "").trim(),
		});
		event.currentTarget.reset();
	};
	return (
		<section className="space-y-6">
			<header>
				<h1 className="text-3xl font-bold">{board.data.title}</h1>
				<p className="mt-2 text-muted-foreground">
					{board.data.locked
						? "This board is read-only."
						: "New discussions are visible to the community."}
				</p>
			</header>
			{!board.data.locked && (
				<Card>
					<CardContent className="pt-6">
						<form className="space-y-3" onSubmit={submit}>
							<Input name="title" required placeholder="Post title" />
							<textarea
								className="min-h-28 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm"
								name="content"
								required
								placeholder="Start the discussion"
							/>
							<Button type="submit" disabled={create.isPending}>
								{create.isPending ? "Posting…" : "Create post"}
							</Button>
							{create.isError && (
								<p className="text-sm text-destructive">Could not create the post.</p>
							)}
						</form>
					</CardContent>
				</Card>
			)}
			<div className="space-y-3">
				{posts.data.length === 0 ? (
					<Card>
						<CardContent className="py-6 text-muted-foreground">
							No posts yet.
						</CardContent>
					</Card>
				) : (
					posts.data.map((post) => (
						<Card key={post.id}>
							<CardHeader>
								<CardTitle>
									<Link
										to="/board/post/$postId"
										params={{ postId: String(post.id) }}
										className="hover:underline"
									>
										{post.title}
									</Link>
								</CardTitle>
								<CardDescription>
									By {post.author} · score {post.score}
								</CardDescription>
							</CardHeader>
							<CardContent className="whitespace-pre-wrap text-sm">
								{post.content}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}
export const boardPostRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/board/post/$postId",
	component: BoardPostPage,
});
function BoardPostPage() {
	const { postId } = useParams({ from: "/board/post/$postId" });
	const id = Number(postId);
	const queryClient = useQueryClient();
	const post = useQuery({
		queryKey: ["community-post", id],
		queryFn: () => pckgApi.getPost(id),
	});
	const comments = useQuery({
		queryKey: ["community-post-comments", id],
		queryFn: () => pckgApi.listPostComments(id),
	});
	const vote = useMutation({
		mutationFn: (value: -1 | 1) => pckgApi.voteOnPost(id, value),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["community-post", id] }),
	});
	const comment = useMutation({
		mutationFn: (content: string) => pckgApi.createComment(id, { content }),
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["community-post-comments", id] }),
	});
	if (post.isPending || comments.isPending)
		return <p className="text-muted-foreground">Loading post…</p>;
	if (post.isError) throw post.error;
	if (comments.isError) throw comments.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const content = String(
			new FormData(event.currentTarget).get("content") ?? "",
		).trim();
		if (content) {
			comment.mutate(content);
			event.currentTarget.reset();
		}
	};
	return (
		<section className="space-y-6">
			<Card>
				<CardHeader>
					<CardTitle>{post.data.title}</CardTitle>
					<CardDescription>
						By {post.data.author} · score {post.data.score}
					</CardDescription>
				</CardHeader>
				<CardContent>
					<p className="whitespace-pre-wrap">{post.data.content}</p>
					<div className="mt-4 flex gap-2">
						<Button size="sm" variant="outline" onClick={() => vote.mutate(1)}>
							Upvote
						</Button>
						<Button size="sm" variant="outline" onClick={() => vote.mutate(-1)}>
							Downvote
						</Button>
					</div>
				</CardContent>
			</Card>
			<section>
				<h2 className="text-xl font-semibold">Comments</h2>
				<div className="mt-3 space-y-3">
					{comments.data.map((item) => (
						<Card key={item.id}>
							<CardContent className="py-4 text-sm">
								<p className="font-medium">
									{item.author} · score {item.score}
								</p>
								<p className="mt-2 whitespace-pre-wrap">{item.content}</p>
							</CardContent>
						</Card>
					))}
				</div>
				<Card className="mt-4">
					<CardContent className="pt-6">
						<form className="space-y-3" onSubmit={submit}>
							<textarea
								className="min-h-24 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm"
								name="content"
								required
								placeholder="Add a comment"
							/>
							<Button type="submit" disabled={comment.isPending}>
								{comment.isPending ? "Posting…" : "Comment"}
							</Button>
							{comment.isError && (
								<p className="text-sm text-destructive">Could not add the comment.</p>
							)}
						</form>
					</CardContent>
				</Card>
			</section>
		</section>
	);
}
