import { describe, expect, it } from "vitest";

import { packageKindPresentation } from "./package-kind-presentation";

describe("packageKindPresentation", () => {
	it("routes library packages to structured documentation", () => {
		expect(
			packageKindPresentation({
				kind: "library",
				packageName: "beskid.core",
				latestVersion: "0.4.0",
			}),
		).toEqual({
			kind: "library",
			badge: "Library package",
			showDocumentation: true,
			instructions: null,
		});
	});

	it("routes template packages to scaffold installation", () => {
		const templatePackage = {
			kind: "template" as const,
			packageName: "beskid.templates.console",
			latestVersion: "0.4.0",
			templateShortName: "console",
		};

		expect(packageKindPresentation(templatePackage)).toEqual({
			kind: "template",
			badge: "Template package",
			showDocumentation: false,
			instructions: {
				title: "Install this template",
				description:
					"Install the verified scaffold, then create a project from it.",
				commands: [
					"beskid new install beskid.templates.console",
					"beskid new console",
				],
			},
		});
	});

	it("routes tool packages to an exact-version artifact download", () => {
		expect(
			packageKindPresentation({
				kind: "tool",
				packageName: "beskid-fmt-extra",
				latestVersion: "0.4.0",
			}),
		).toEqual({
			kind: "tool",
			badge: "Tool package",
			showDocumentation: false,
			instructions: {
				title: "Install this tool",
				description: "Download the verified tool artifact from the registry.",
				commands: [
					"beskid pckg download beskid-fmt-extra --version 0.4.0 --output ./beskid-fmt-extra.bpk",
				],
			},
		});
	});

	it("does not invent a tool command before a release exists", () => {
		expect(
			packageKindPresentation({
				kind: "tool",
				packageName: "beskid-fmt-extra",
				latestVersion: null,
			}),
		).toMatchObject({
			showDocumentation: false,
			instructions: {
				commands: [],
			},
		});
	});

	it("fails closed when the registry returns an unsupported kind", () => {
		expect(() =>
			packageKindPresentation({
				kind: "plugin" as never,
				packageName: "untrusted-package",
				latestVersion: "0.4.0",
			}),
		).toThrowError("Unsupported package kind: plugin");
	});
});
