#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${root}/Dockerfile"

server_stage="$(sed -n '/^FROM mcr.microsoft.com\/dotnet\/sdk:10.0 AS server-build$/,/^FROM mcr.microsoft.com\/dotnet\/aspnet:10.0 AS final$/p' "${dockerfile}")"
if [[ "${server_stage}" != *'dotnet publish src/Server/Server.csproj'* ]]; then
    echo "pckg server image must publish the repository's Server.csproj application" >&2
    exit 1
fi
