#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${root}/Dockerfile"

web_stage="$(sed -n '/^FROM node:.* AS web-build$/,/^FROM mcr.microsoft.com\/dotnet\/sdk:10.0 AS server-build$/p' "${dockerfile}")"
if [[ "${web_stage}" != *'beskid_web_common'* ]]; then
	echo "pckg web-build must copy/install beskid_web_common before consumer pnpm install" >&2
	exit 1
fi
if [[ "${web_stage}" != *'pnpm install --dir /src/beskid_web_common --frozen-lockfile'* ]]; then
	echo "pckg web-build must frozen-install beskid_web_common first" >&2
	exit 1
fi
if [[ "${web_stage}" != *'pnpm install --dir /src/pckg/web --frozen-lockfile'* ]]; then
	echo "pckg web-build must use pnpm install --frozen-lockfile for pckg/web" >&2
	exit 1
fi
if [[ "${web_stage}" == *'bun install'* || "${web_stage}" == *'oven/bun'* ]]; then
	echo "pckg web-build must not use Bun; the web build is migrated to pnpm" >&2
	exit 1
fi

server_stage="$(sed -n '/^FROM mcr.microsoft.com\/dotnet\/sdk:10.0 AS server-build$/,/^FROM mcr.microsoft.com\/dotnet\/aspnet:10.0 AS final$/p' "${dockerfile}")"
if [[ "${server_stage}" != *'dotnet publish src/Server/Server.csproj'* ]]; then
    echo "pckg server image must publish the repository's Server.csproj application" >&2
    exit 1
fi
