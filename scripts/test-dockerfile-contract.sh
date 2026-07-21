#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${root}/Dockerfile"

web_stage="$(sed -n '/^FROM oven\/bun:.* AS web-build$/,/^FROM mcr.microsoft.com\/dotnet\/sdk:10.0 AS server-build$/p' "${dockerfile}")"
if [[ "${web_stage}" != *'COPY beskid_web_common /src/beskid_web_common'* ]]; then
	echo "pckg web-build must copy beskid_web_common before bun install (file: @beskid/* deps)" >&2
	exit 1
fi
if [[ "${web_stage}" != *'bun install --frozen-lockfile'* ]]; then
	echo "pckg web-build must use bun install --frozen-lockfile" >&2
	exit 1
fi
if [[ "${web_stage}" == *'|| bun install'* ]]; then
	echo "pckg web-build must not fall back to an unfrozen bun install" >&2
	exit 1
fi

server_stage="$(sed -n '/^FROM mcr.microsoft.com\/dotnet\/sdk:10.0 AS server-build$/,/^FROM mcr.microsoft.com\/dotnet\/aspnet:10.0 AS final$/p' "${dockerfile}")"
if [[ "${server_stage}" != *'dotnet publish src/Server/Server.csproj'* ]]; then
    echo "pckg server image must publish the repository's Server.csproj application" >&2
    exit 1
fi
