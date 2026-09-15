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
import { useState } from "react";

import { buildAuthentikLoginUrl } from "../lib/auth-navigation";
import { pckgApi } from "../lib/pckg-api";
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
	const startSignIn = () => {
		window.location.assign(buildAuthentikLoginUrl(window.location.origin, next));
	};
	return (
		<AuthPageShell
			title="Sign in to pckg"
			description="Continue with GitHub through Beskid Authentik to manage packages."
		>
			<Button onClick={startSignIn}>
				Continue with GitHub
			</Button>
			<p className="mt-4 text-sm text-muted-foreground">
				You will return to {next} after authentication.
			</p>
		</AuthPageShell>
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
						<label htmlFor="api-key-name" className="grid gap-2 text-sm font-medium">
							Name
							<Input
								id="api-key-name"
								name="name"
								required
								placeholder="CI publishing"
							/>
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
