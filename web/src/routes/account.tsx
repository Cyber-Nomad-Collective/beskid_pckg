import { AuthPageShell } from "@beskid/ui-react/auth";
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
import { createRoute, useSearch } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import { buildAuthHubLoginUrl } from "../lib/auth-navigation";
import { PckgApiError, pckgApi } from "../lib/pckg-api";
import { dashboardRoute } from "./dashboard";
import { rootRoute } from "./shared";

export const authRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/auth",
	validateSearch: (search: Record<string, unknown>) => ({
		next:
			typeof search.next === "string" ? search.next : "/dashboard/packages/my",
	}),
	component: AuthPage,
});
function AuthPage() {
	const { next } = useSearch({ from: "/auth" });
	const authHubUrl = import.meta.env.VITE_AUTH_HUB_PUBLIC_URL;
	const startSignIn = () => {
		if (authHubUrl) window.location.assign(buildAuthHubLoginUrl(authHubUrl));
	};
	return (
		<AuthPageShell
			title="Sign in to pckg"
			description="Continue with GitHub through Beskid Auth Hub to manage packages."
		>
			<Button onClick={startSignIn} disabled={!authHubUrl}>
				Continue with GitHub
			</Button>
			<p className="mt-4 text-sm text-muted-foreground">
				You will return to {next} after authentication.
			</p>
			{!authHubUrl && (
				<p className="mt-3 text-sm text-destructive">Auth Hub is not configured.</p>
			)}
		</AuthPageShell>
	);
}

export const settingsAuthPairRoute = createRoute({
	getParentRoute: () => rootRoute,
	path: "/settings/auth/pair",
	validateSearch: (search: Record<string, unknown>) => ({
		code: typeof search.code === "string" ? search.code : "",
	}),
	component: AuthHubPairingPage,
});

function AuthHubPairingPage() {
	const { code: initialCode } = useSearch({ from: "/settings/auth/pair" });
	const [code, setCode] = useState(initialCode);
	const [publicUrl, setPublicUrl] = useState("");
	const [statusMessage, setStatusMessage] = useState<string | null>(null);
	const [errorMessage, setErrorMessage] = useState<string | null>(null);
	const pairStatus = useQuery({
		queryKey: ["auth-hub-pairing-status"],
		queryFn: () => pckgApi.getAuthHubPairingStatus(),
	});
	const pair = useMutation({
		mutationFn: (input: { code: string; publicUrl: string }) =>
			pckgApi.pairWithAuthHub(input),
	});

	useEffect(() => {
		if (pairStatus.data) {
			setPublicUrl(pairStatus.data.defaultPublicUrl);
		}
	}, [pairStatus.data]);

	useEffect(() => {
		if (
			!pairStatus.data ||
			pairStatus.data.paired ||
			pair.isPending ||
			!code ||
			!publicUrl
		) {
			return;
		}
		void pair.mutateAsync({ code, publicUrl });
	}, [pairStatus.data, code, publicUrl, pair]);

	if (pairStatus.isPending) {
		return <p className="text-muted-foreground">Checking pairing state…</p>;
	}
	if (pairStatus.isError) throw pairStatus.error;

	const onSubmit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		setErrorMessage(null);
		setStatusMessage(null);
		pair.mutate(
			{ code, publicUrl },
			{
				onSuccess: (result) => {
					if (result.alreadyPaired) {
						setStatusMessage("pckg is already paired with the auth hub.");
					} else {
						setStatusMessage("Auth hub paired successfully.");
					}
				},
				onError: (error) => {
					if (error instanceof PckgApiError && error.status === 401) {
						setErrorMessage("Sign in as SuperAdmin to approve pairing.");
					} else {
						setErrorMessage(
							error instanceof Error ? error.message : "Pairing failed.",
						);
					}
				},
			},
		);
	};

	if (pairStatus.data.paired) {
		return (
			<section className="mx-auto max-w-2xl space-y-4">
				<h1 className="text-3xl font-bold">Auth hub pairing</h1>
				<p className="text-muted-foreground">
					pckg is already paired with the auth hub.
				</p>
			</section>
		);
	}

	return (
		<section className="mx-auto max-w-2xl space-y-4">
			<h1 className="text-3xl font-bold">Auth hub pairing</h1>
			<p className="text-muted-foreground">
				Approve a pairing code from the auth hub admin. The service token is stored
				on the server and never shown in the browser.
			</p>
			{statusMessage && (
				<p className="text-sm text-emerald-600">{statusMessage}</p>
			)}
			{errorMessage && <p className="text-sm text-destructive">{errorMessage}</p>}
			<Card>
				<CardContent className="pt-6">
					<form className="space-y-3" onSubmit={onSubmit}>
						<label className="grid gap-2 text-sm">
							Pairing code
							<Input
								name="code"
								required
								value={code}
								onChange={(event) => setCode(event.target.value)}
							/>
						</label>
						<label className="grid gap-2 text-sm">
							This app public URL
							<Input
								name="publicUrl"
								placeholder="https://pckg.beskid-lang.org"
								required
								value={publicUrl}
								onChange={(event) => setPublicUrl(event.target.value)}
							/>
						</label>
						<Button type="submit" disabled={pair.isPending}>
							{pair.isPending ? "Approving…" : "Approve pairing"}
						</Button>
					</form>
				</CardContent>
			</Card>
		</section>
	);
}

export const profileRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/profile",
	component: ProfilePage,
});
function ProfilePage() {
	const session = useQuery({
		queryKey: ["session"],
		queryFn: () => pckgApi.getSession(),
	});
	const profile = useQuery({
		queryKey: ["community-profile", "me"],
		enabled: Boolean(session.data),
		queryFn: () => {
			const subject = session.data?.subject;
			if (!subject)
				throw new Error("An authenticated session is required to load a profile.");
			return pckgApi.getCommunityProfile(subject);
		},
		retry: false,
	});
	const update = useMutation({ mutationFn: pckgApi.updateMyCommunityProfile });
	if (session.isPending || profile.isPending)
		return <p className="text-muted-foreground">Loading profile…</p>;
	if (session.isError) throw session.error;
	const initial = profile.data;
	const submit = async (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		await update.mutateAsync({
			display_name: String(form.get("displayName") ?? "").trim(),
			bio: String(form.get("bio") ?? ""),
			social_links: String(form.get("socialLinks") ?? "")
				.split("\n")
				.map((link) => link.trim())
				.filter(Boolean),
		});
	};
	return (
		<section className="max-w-2xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Profile settings</h1>
				<p className="mt-2 text-muted-foreground">
					Signed in as {session.data?.githubLogin}. This profile is keyed by your
					GitHub-backed Auth Hub subject.
				</p>
			</header>
			<Card>
				<CardContent className="pt-6">
					<form className="space-y-4" onSubmit={submit}>
						<label className="grid gap-2 text-sm font-medium">
							Display name
							<Input
								name="displayName"
								required
								defaultValue={initial?.display_name ?? session.data?.githubLogin ?? ""}
							/>
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Biography
							<textarea
								className="min-h-24 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
								name="bio"
								defaultValue={initial?.bio ?? ""}
							/>
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Social links{" "}
							<span className="font-normal text-muted-foreground">
								(one URL per line)
							</span>
							<textarea
								className="min-h-24 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
								name="socialLinks"
								defaultValue={initial?.social_links.join("\n") ?? ""}
							/>
						</label>
						{profile.isError &&
							!(
								profile.error instanceof PckgApiError && profile.error.status === 404
							) && (
								<p className="text-sm text-destructive">
									Could not load the existing profile.
								</p>
							)}
						{update.isError && (
							<p className="text-sm text-destructive">Could not save the profile.</p>
						)}
						<Button type="submit" disabled={update.isPending}>
							{update.isPending ? "Saving…" : "Save profile"}
						</Button>
					</form>
				</CardContent>
			</Card>
		</section>
	);
}

export const notificationsRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/notifications",
	component: NotificationsPage,
});
function NotificationsPage() {
	const queryClient = useQueryClient();
	const notifications = useQuery({
		queryKey: ["notifications"],
		queryFn: () => pckgApi.listNotifications(),
	});
	const preference = useMutation({
		mutationFn: pckgApi.updateNotificationPreference,
	});
	const markRead = useMutation({
		mutationFn: pckgApi.markNotificationRead,
		onSuccess: () =>
			queryClient.invalidateQueries({ queryKey: ["notifications"] }),
	});
	if (notifications.isPending)
		return <p className="text-muted-foreground">Loading notifications…</p>;
	if (notifications.isError) throw notifications.error;
	return (
		<section className="space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Notifications</h1>
				<p className="mt-2 text-muted-foreground">
					Control community notifications and mark messages read when you have
					handled them.
				</p>
			</header>
			<Card>
				<CardContent className="py-5">
					<div className="flex flex-wrap gap-2">
						<Button
							variant="outline"
							disabled={preference.isPending}
							onClick={() => preference.mutate("all")}
						>
							All community notifications
						</Button>
						<Button
							variant="outline"
							disabled={preference.isPending}
							onClick={() => preference.mutate("mentionsOnly")}
						>
							Mentions only
						</Button>
					</div>
					{preference.isError && (
						<p className="mt-3 text-sm text-destructive">
							Could not update notification preference.
						</p>
					)}
				</CardContent>
			</Card>
			<div className="space-y-3">
				{notifications.data.length === 0 ? (
					<Card>
						<CardContent className="py-6 text-muted-foreground">
							No notifications.
						</CardContent>
					</Card>
				) : (
					notifications.data.map((notice) => (
						<Card key={notice.id}>
							<CardContent className="flex flex-wrap items-center justify-between gap-3 py-4 text-sm">
								<p>
									<strong>{notice.actor}</strong> triggered a{" "}
									<strong>{notice.scope}</strong> notification
									{notice.post_id !== null ? ` on post ${notice.post_id}` : ""}
									{notice.comment_id !== null ? ` in comment ${notice.comment_id}` : ""}.
								</p>
								{!notice.is_read && (
									<Button
										size="sm"
										variant="outline"
										disabled={markRead.isPending}
										onClick={() => markRead.mutate(notice.id)}
									>
										Mark read
									</Button>
								)}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}
export const apiKeysRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/api-keys",
	component: ApiKeysPage,
});
function ApiKeysPage() {
	const queryClient = useQueryClient();
	const [createdKey, setCreatedKey] = useState<string | null>(null);
	const keys = useQuery({
		queryKey: ["api-keys"],
		queryFn: () => pckgApi.listApiKeys(),
	});
	const create = useMutation({
		mutationFn: (input: { name: string; scopes: string[] }) =>
			pckgApi.createApiKey(input),
		onSuccess: (result) => {
			setCreatedKey(result.plainTextKey);
			void queryClient.invalidateQueries({ queryKey: ["api-keys"] });
		},
	});
	const revoke = useMutation({
		mutationFn: (keyId: string) => pckgApi.revokeApiKey(keyId),
		onSuccess: () => queryClient.invalidateQueries({ queryKey: ["api-keys"] }),
	});
	if (keys.isPending)
		return <p className="text-muted-foreground">Loading API keys…</p>;
	if (keys.isError) throw keys.error;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const scopes = ["read", "publish"].filter(
			(scope) => form.get(scope) === "on",
		);
		create.mutate({ name: String(form.get("name") ?? "").trim(), scopes });
	};
	return (
		<section className="max-w-3xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">API keys</h1>
				<p className="mt-2 text-muted-foreground">
					Create narrowly scoped credentials for local tools and CI. The secret is
					displayed only once.
				</p>
			</header>
			{createdKey && (
				<Card>
					<CardHeader>
						<CardTitle>Copy this key now</CardTitle>
						<CardDescription>
							It cannot be recovered after this message is dismissed.
						</CardDescription>
					</CardHeader>
					<CardContent className="space-y-3">
						<code className="block overflow-x-auto rounded-md border border-border bg-muted p-3 text-sm">
							{createdKey}
						</code>
						<Button variant="outline" onClick={() => setCreatedKey(null)}>
							I copied it
						</Button>
					</CardContent>
				</Card>
			)}
			<Card>
				<CardHeader>
					<CardTitle>Create API key</CardTitle>
				</CardHeader>
				<CardContent className="pt-1">
					<form className="space-y-4" onSubmit={submit}>
						<label className="grid gap-2 text-sm font-medium">
							Name
							<Input name="name" required placeholder="CI publishing" />
						</label>
						<fieldset className="space-y-2">
							<legend className="text-sm font-medium">Scopes</legend>
							<label className="flex items-center gap-2 text-sm">
								<input name="read" type="checkbox" defaultChecked />
								Read public and permitted package data
							</label>
							<label className="flex items-center gap-2 text-sm">
								<input name="publish" type="checkbox" defaultChecked />
								Publish package versions
							</label>
						</fieldset>
						{create.isError && (
							<p className="text-sm text-destructive">
								Could not create this API key. Check its name and scopes.
							</p>
						)}
						<Button type="submit" disabled={create.isPending}>
							{create.isPending ? "Creating…" : "Create API key"}
						</Button>
					</form>
				</CardContent>
			</Card>
			<div className="space-y-3">
				{keys.data.length === 0 ? (
					<Card>
						<CardContent className="py-6 text-muted-foreground">
							No API keys yet.
						</CardContent>
					</Card>
				) : (
					keys.data.map((key) => (
						<Card key={key.id}>
							<CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
								<div>
									<p className="font-medium">{key.name}</p>
									<p className="mt-1 text-sm text-muted-foreground">
										<code>{key.prefix}</code> · {key.scopes.join(", ")} · created{" "}
										{new Date(key.createdAtUtc).toLocaleDateString()}
										{key.revokedAtUtc
											? ` · revoked ${new Date(key.revokedAtUtc).toLocaleDateString()}`
											: ""}
									</p>
								</div>
								{!key.revokedAtUtc && (
									<Button
										size="sm"
										variant="outline"
										disabled={revoke.isPending}
										onClick={() => revoke.mutate(key.id)}
									>
										Revoke
									</Button>
								)}
							</CardContent>
						</Card>
					))
				)}
			</div>
			{revoke.isError && (
				<p className="text-sm text-destructive">Could not revoke this API key.</p>
			)}
		</section>
	);
}
