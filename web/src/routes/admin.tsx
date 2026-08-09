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
import { createRoute, Link } from "@tanstack/react-router";

import { PckgApiError, pckgApi } from "../lib/pckg-api";
import { dashboardRoute } from "./dashboard";

export const adminEmailRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin/email",
	component: AdminEmailPage,
});
export const adminRegistryActivityRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin/registry-activity",
	component: AdminRegistryActivityPage,
});
export const adminBlockedLinksRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin/blocked-links",
	component: AdminBlockedLinksPage,
});

function AdminEmailPage() {
	const queryClient = useQueryClient();
	const settingsQuery = useQuery({
		queryKey: ["admin-email-settings"],
		queryFn: () => pckgApi.getEmailSettings(),
	});
	const update = useMutation({
		mutationFn: pckgApi.updateEmailSettings,
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["admin-email-settings"] }),
	});
	if (settingsQuery.isPending) {
		return <p className="text-muted-foreground">Loading email settings…</p>;
	}
	if (settingsQuery.isError) throw settingsQuery.error;
	const settings = settingsQuery.data;
	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		update.mutate({
			smtpHost: String(form.get("smtpHost") ?? "").trim() || null,
			smtpPort: Number(form.get("smtpPort")),
			enableSsl: form.get("enableSsl") === "on",
			username: String(form.get("username") ?? "").trim() || null,
			password: String(form.get("password") ?? "").trim() || null,
			fromEmail: String(form.get("fromEmail") ?? "").trim(),
			fromName: String(form.get("fromName") ?? "").trim(),
		});
	};

	return (
		<section className="space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Email settings</h1>
				<p className="mt-2 text-muted-foreground">
					Manage SMTP settings used for notification and account flows.
				</p>
			</header>
			<Card>
				<CardContent className="pt-6">
					<form className="space-y-4" onSubmit={submit}>
						<label className="grid gap-2 text-sm font-medium">
							SMTP host
							<Input name="smtpHost" required defaultValue={settings.smtpHost ?? ""} />
						</label>
						<label className="grid gap-2 text-sm font-medium">
							SMTP port
							<Input name="smtpPort" type="number" defaultValue={settings.smtpPort} />
						</label>
						<label className="flex items-center gap-2 text-sm">
							<input
								name="enableSsl"
								type="checkbox"
								defaultChecked={settings.enableSsl}
							/>
							Use TLS
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Username
							<Input name="username" defaultValue={settings.username ?? ""} />
						</label>
						<label className="grid gap-2 text-sm font-medium">
							Password
							<Input
								name="password"
								type="password"
								placeholder="Update password only when changed"
								defaultValue={settings.password ?? ""}
							/>
						</label>
						<label className="grid gap-2 text-sm font-medium">
							From email
							<Input name="fromEmail" required defaultValue={settings.fromEmail} />
						</label>
						<label className="grid gap-2 text-sm font-medium">
							From name
							<Input name="fromName" required defaultValue={settings.fromName} />
						</label>
						{update.isError && (
							<p className="text-sm text-destructive">
								Could not save email settings.
							</p>
						)}
						<Button type="submit" disabled={update.isPending}>
							{update.isPending ? "Saving…" : "Save email settings"}
						</Button>
					</form>
				</CardContent>
			</Card>
		</section>
	);
}

function AdminRegistryActivityPage() {
	const activity = useQuery({
		queryKey: ["admin-registry-activity"],
		queryFn: () => pckgApi.listRegistryActivity(200),
	});
	if (activity.isPending) {
		return <p className="text-muted-foreground">Loading registry activity…</p>;
	}
	if (activity.isError) throw activity.error;
	return (
		<section className="space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Registry activity</h1>
				<p className="mt-2 text-muted-foreground">
					Recent package registry actions, including publish and moderation events.
				</p>
			</header>
			<div className="space-y-3">
				{activity.data.length === 0 ? (
					<Card>
						<CardContent className="py-5 text-muted-foreground">
							No recent registry activity.
						</CardContent>
					</Card>
				) : (
					activity.data.map((item) => (
						<Card key={`${item.traceId || "activity"}-${item.timestampUtc}`}>
							<CardContent className="py-4 text-sm">
								<div className="flex flex-wrap justify-between gap-2">
									<span>{new Date(item.timestampUtc).toLocaleString()}</span>
									<span className="rounded bg-muted px-2 py-0.5 text-xs">
										{item.severity}
									</span>
								</div>
								<p className="mt-2 font-medium">{item.action}</p>
								<p>{item.message}</p>
								{item.packageName && (
									<p className="mt-2 text-muted-foreground">
										{item.packageName}
										{item.version ? ` ${item.version}` : ""}
									</p>
								)}
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}

function AdminBlockedLinksPage() {
	const queryClient = useQueryClient();
	const blocked = useQuery({
		queryKey: ["admin-blocked-links"],
		queryFn: () => pckgApi.listBlockedLinks(),
	});
	const add = useMutation({
		mutationFn: pckgApi.addBlockedLink,
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["admin-blocked-links"] }),
	});
	const remove = useMutation({
		mutationFn: pckgApi.deleteBlockedLink,
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["admin-blocked-links"] }),
	});
	if (blocked.isPending) {
		return <p className="text-muted-foreground">Loading blocked links…</p>;
	}
	if (blocked.isError) throw blocked.error;

	const submit = (event: React.FormEvent<HTMLFormElement>) => {
		event.preventDefault();
		const form = new FormData(event.currentTarget);
		const pattern = String(form.get("pattern") ?? "").trim();
		const note = String(form.get("note") ?? "").trim();
		add.mutate({ pattern, note: note || undefined });
		event.currentTarget.reset();
	};

	return (
		<section className="space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Blocked links</h1>
				<p className="mt-2 text-muted-foreground">
					Store link patterns blocked from outgoing moderation actions.
				</p>
			</header>
			<Card>
				<CardContent className="pt-6">
					<form className="grid gap-3" onSubmit={submit}>
						<label className="grid gap-2 text-sm font-medium">
							Pattern
							<Input name="pattern" required placeholder="https://bad.example/*" />
						</label>
						<textarea
							name="note"
							className="min-h-20 rounded-md border border-input bg-transparent px-3 py-2 text-sm"
							placeholder="Optional note"
						/>
						<Button type="submit" disabled={add.isPending}>
							{add.isPending ? "Adding…" : "Add pattern"}
						</Button>
					</form>
					{add.isError && (
						<p className="mt-3 text-sm text-destructive">
							Could not add this blocked link.
						</p>
					)}
				</CardContent>
			</Card>
			<div className="space-y-3">
				{blocked.data.length === 0 ? (
					<Card>
						<CardContent className="py-6 text-muted-foreground">
							No blocked link patterns.
						</CardContent>
					</Card>
				) : (
					blocked.data.map((link) => (
						<Card key={link.id}>
							<CardContent className="flex flex-wrap gap-2 py-4">
								<div className="flex-1">
									<code className="break-all">{link.pattern}</code>
									{link.note ? (
										<p className="mt-1 text-sm text-muted-foreground">{link.note}</p>
									) : null}
								</div>
								<Button
									size="sm"
									variant="outline"
									disabled={remove.isPending}
									onClick={() => remove.mutate(link.id)}
								>
									Delete
								</Button>
							</CardContent>
						</Card>
					))
				)}
			</div>
		</section>
	);
}

function adminErrorMessage(error: unknown): string {
	if (!(error instanceof PckgApiError))
		return "The registry could not complete this administrative request.";
	if (error.status === 401)
		return "Your Auth Hub session has expired. Sign in again to continue.";
	if (error.status === 403)
		return "Your GitHub-backed account does not have permission to administer the registry.";
	if (error.status === 404)
		return "The requested administrative record no longer exists.";
	return "The registry could not complete this administrative request.";
}

export const adminRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin",
	component: AdminOverviewPage,
});
function AdminOverviewPage() {
	const queryClient = useQueryClient();
	const users = useQuery({
		queryKey: ["admin-users"],
		queryFn: () => pckgApi.listAdminUsers(),
	});
	const permissions = useQuery({
		queryKey: ["admin-permissions"],
		queryFn: () => pckgApi.listAdminPermissions(),
	});
	const grant = useMutation({
		mutationFn: pckgApi.grantAdminPermission,
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["admin-permissions"] }),
	});
	if (users.isPending || permissions.isPending)
		return <p className="text-muted-foreground">Loading administration…</p>;
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
	return (
		<section className="max-w-4xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Administration</h1>
				<p className="mt-2 text-muted-foreground">
					Manage GitHub-subject registry roles, publisher verification, and narrowly
					scoped resource permissions.
				</p>
			</header>
			<div className="grid gap-4 sm:grid-cols-2">
				<Card>
					<CardHeader>
						<CardTitle>{users.data.length} users</CardTitle>
						<CardDescription>
							Roles and publisher verification are managed by immutable GitHub
							subjects.
						</CardDescription>
					</CardHeader>
					<CardContent>
						<Link
							to="/dashboard/admin/users"
							className={buttonVariants({ variant: "outline" })}
						>
							Manage users
						</Link>
					</CardContent>
				</Card>
				<Card>
					<CardHeader>
						<CardTitle>{permissions.data.length} permissions</CardTitle>
						<CardDescription>
							Explicit grants supplement the standard role policy for a resource.
						</CardDescription>
					</CardHeader>
				</Card>
			</div>
			<Card>
				<CardHeader>
					<CardTitle>Grant resource permission</CardTitle>
					<CardDescription>
						Use a GitHub subject, such as <code>github:42</code>, and a
						server-recognized resource identifier.
					</CardDescription>
				</CardHeader>
				<CardContent>
					<form
						className="grid gap-3 md:grid-cols-[1fr_1fr_10rem_auto]"
						onSubmit={submit}
					>
						<Input
							name="subject"
							required
							pattern="github:[0-9]+"
							placeholder="github:42"
							aria-label="GitHub subject"
						/>
						<Input
							name="resource"
							required
							placeholder="package:beskid.http"
							aria-label="Resource"
						/>
						<select
							name="capability"
							className="h-9 rounded-md border border-input bg-transparent px-3 text-sm"
							aria-label="Capability"
						>
							<option value="moderate">Moderate</option>
							<option value="manage">Manage</option>
						</select>
						<Button type="submit" disabled={grant.isPending}>
							{grant.isPending ? "Granting…" : "Grant"}
						</Button>
					</form>
					{grant.isError && (
						<p className="mt-3 text-sm text-destructive">
							{adminErrorMessage(grant.error)}
						</p>
					)}
				</CardContent>
			</Card>
			<section>
				<h2 className="text-xl font-semibold">Current permissions</h2>
				<div className="mt-3 space-y-3">
					{permissions.data.length === 0 ? (
						<Card>
							<CardContent className="py-5 text-sm text-muted-foreground">
								No explicit permissions have been granted.
							</CardContent>
						</Card>
					) : (
						permissions.data.map((permission) => (
							<Card
								key={`${permission.subject}:${permission.resource}:${permission.capability}`}
							>
								<CardContent className="py-4 text-sm">
									<code>{permission.subject}</code> can{" "}
									<strong>{permission.capability}</strong>{" "}
									<code>{permission.resource}</code>.
								</CardContent>
							</Card>
						))
					)}
				</div>
			</section>
		</section>
	);
}

export const adminUsersRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin/users",
	component: AdminUsersPage,
});
export const boardModerationRoute = createRoute({
	getParentRoute: () => dashboardRoute,
	path: "/admin/boards",
	component: BoardModerationPage,
});
function BoardModerationPage() {
	const queryClient = useQueryClient();
	const boards = useQuery({
		queryKey: ["community-boards"],
		queryFn: () => pckgApi.listBoards(),
	});
	const setLocked = useMutation({
		mutationFn: ({ id, locked }: { id: string; locked: boolean }) =>
			pckgApi.setBoardLocked(id, locked),
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["community-boards"] }),
	});
	if (boards.isPending)
		return <p className="text-muted-foreground">Loading boards…</p>;
	if (boards.isError) throw boards.error;
	return (
		<section className="max-w-3xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Board moderation</h1>
				<p className="mt-2 text-muted-foreground">
					Lock a board to pause new discussions. The registry enforces your moderator
					or delegated board permission.
				</p>
			</header>
			<div className="space-y-3">
				{boards.data.map((board) => (
					<Card key={board.id}>
						<CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
							<div>
								<p className="font-medium">{board.title}</p>
								<p className="text-sm text-muted-foreground">
									{board.locked
										? "Locked — members cannot post."
										: "Open for discussion."}
								</p>
							</div>
							<Button
								variant="outline"
								disabled={setLocked.isPending}
								onClick={() =>
									setLocked.mutate({ id: board.id, locked: !board.locked })
								}
							>
								{board.locked ? "Unlock board" : "Lock board"}
							</Button>
						</CardContent>
					</Card>
				))}
			</div>
			{setLocked.isError && (
				<p className="text-sm text-destructive">
					You do not have permission to change this board, or the registry could not
					save it.
				</p>
			)}
		</section>
	);
}
function AdminUsersPage() {
	const queryClient = useQueryClient();
	const users = useQuery({
		queryKey: ["admin-users"],
		queryFn: () => pckgApi.listAdminUsers(),
	});
	const update = useMutation({
		mutationFn: ({
			subject,
			roles,
			publisherVerified,
		}: {
			subject: string;
			roles: string[];
			publisherVerified: boolean;
		}) => pckgApi.updateAdminUser(subject, { roles, publisherVerified }),
		onSuccess: () =>
			void queryClient.invalidateQueries({ queryKey: ["admin-users"] }),
	});
	if (users.isPending)
		return <p className="text-muted-foreground">Loading registry users…</p>;
	if (users.isError) throw users.error;
	return (
		<section className="max-w-4xl space-y-6">
			<header>
				<h1 className="text-3xl font-bold">Users and roles</h1>
				<p className="mt-2 text-muted-foreground">
					Changes apply to the GitHub subject shown for each account. Email addresses
					and local passwords are never used.
				</p>
			</header>
			{users.data.length === 0 ? (
				<Card>
					<CardContent className="py-6 text-muted-foreground">
						No registry users are available.
					</CardContent>
				</Card>
			) : (
				<div className="space-y-4">
					{users.data.map((user) => (
						<Card key={user.subject}>
							<CardHeader>
								<CardTitle>{user.githubLogin}</CardTitle>
								<CardDescription>
									<code>{user.subject}</code>
								</CardDescription>
							</CardHeader>
							<CardContent>
								<form
									className="flex flex-wrap items-end justify-between gap-4"
									onSubmit={(event) => {
										event.preventDefault();
										const form = new FormData(event.currentTarget);
										update.mutate({
											subject: user.subject,
											roles: ["Member", "Moderator", "SuperAdmin"].filter(
												(role) => form.get(role) === "on",
											),
											publisherVerified: form.get("publisherVerified") === "on",
										});
									}}
								>
									<fieldset className="flex flex-wrap gap-x-4 gap-y-2">
										<legend className="mb-2 text-sm font-medium">Roles</legend>
										{["Member", "Moderator", "SuperAdmin"].map((role) => (
											<label key={role} className="flex items-center gap-2 text-sm">
												<input
													name={role}
													type="checkbox"
													defaultChecked={user.roles.includes(role)}
												/>
												{role}
											</label>
										))}
										<label className="flex items-center gap-2 text-sm">
											<input
												name="publisherVerified"
												type="checkbox"
												defaultChecked={user.publisherVerified}
											/>
											Verified publisher
										</label>
									</fieldset>
									<Button type="submit" disabled={update.isPending}>
										{update.isPending ? "Saving…" : "Save changes"}
									</Button>
								</form>
								{update.isError && (
									<p className="mt-3 text-sm text-destructive">
										{adminErrorMessage(update.error)}
									</p>
								)}
							</CardContent>
						</Card>
					))}
				</div>
			)}
		</section>
	);
}
