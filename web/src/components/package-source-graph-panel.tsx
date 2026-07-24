"use client";

import { type RepoEntry, RepoExplorerDialog } from "@beskid/ui-react/explorer";
import {
	LinkedAstFactsView,
	sampleAst,
	sampleFacts,
	sampleRepo,
} from "@beskid/ui-react/graph";
import { Button } from "@beskid/ui-react/ui/button";
import {
	Card,
	CardContent,
	CardDescription,
	CardHeader,
	CardTitle,
} from "@beskid/ui-react/ui/card";
import { useMemo, useState } from "react";

export type PackageSourceEntry = {
	path: string;
	sizeBytes: number;
};

function sourceEntriesToRepoTree(entries: PackageSourceEntry[]): RepoEntry[] {
	if (entries.length === 0) return [sampleRepo];

	const root: RepoEntry = { path: "", kind: "dir", name: "/", children: [] };
	const dirMap = new Map<string, RepoEntry>([["", root]]);

	const ensureDir = (dirPath: string): RepoEntry => {
		const existing = dirMap.get(dirPath);
		if (existing) return existing;
		const parentPath = dirPath.includes("/")
			? dirPath.slice(0, dirPath.lastIndexOf("/"))
			: "";
		const parent = ensureDir(parentPath);
		const name = dirPath.split("/").pop() || dirPath;
		const dir: RepoEntry = { path: dirPath, kind: "dir", name, children: [] };
		parent.children = [...(parent.children ?? []), dir];
		dirMap.set(dirPath, dir);
		return dir;
	};

	for (const entry of entries) {
		const parts = entry.path.split("/").filter(Boolean);
		const fileName = parts.pop();
		if (!fileName) continue;
		const dirPath = parts.join("/");
		const dir = ensureDir(dirPath);
		dir.children = [
			...(dir.children ?? []).filter((c) => c.path !== entry.path),
			{ path: entry.path, kind: "file", name: fileName },
		];
	}

	return root.children?.length ? root.children : [sampleRepo];
}

export function PackageSourceGraphPanel({
	sourceEntries,
	selectedPath,
	onSelectPath,
}: {
	sourceEntries: PackageSourceEntry[];
	selectedPath: string | null;
	onSelectPath: (path: string) => void;
}) {
	const [explorerOpen, setExplorerOpen] = useState(false);
	const entries = useMemo(
		() => sourceEntriesToRepoTree(sourceEntries),
		[sourceEntries],
	);
	const showGraph = Boolean(selectedPath?.endsWith(".bs"));

	return (
		<div className="space-y-4">
			<div className="flex flex-wrap items-center gap-2">
				<Button
					type="button"
					variant="outline"
					onClick={() => setExplorerOpen(true)}
				>
					Browse source…
				</Button>
				{selectedPath ? (
					<p className="font-mono text-xs text-muted-foreground">{selectedPath}</p>
				) : (
					<p className="text-sm text-muted-foreground">
						Select a `.bs` file to preview AST / facts (fixture until live models
						exist).
					</p>
				)}
			</div>

			<RepoExplorerDialog
				open={explorerOpen}
				onOpenChange={setExplorerOpen}
				entries={entries}
				title="Package source"
				description="Pick a source file from this package version."
				onSelect={(entry) => {
					if (entry.kind === "file") onSelectPath(entry.path);
				}}
			/>

			{showGraph ? (
				<Card>
					<CardHeader>
						<CardTitle>AST and facts</CardTitle>
						<CardDescription>
							Fixture graph for <span className="font-mono">{selectedPath}</span>. Live
							compiler models will use the same viewers later.
						</CardDescription>
					</CardHeader>
					<CardContent>
						<LinkedAstFactsView
							ast={sampleAst}
							facts={sampleFacts}
							openInEditor={{
								githubRepo: "Cyber-Nomad-Collective/beskid",
								githubRef: "main",
							}}
						/>
					</CardContent>
				</Card>
			) : null}
		</div>
	);
}
