#!/usr/bin/env bash
# Static contract for the root-context Rust pckg image.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${root}/Dockerfile"

require() {
	local needle="$1"
	local explanation="$2"
	if ! rg -Fq -- "$needle" "$dockerfile"; then
		echo "pckg Dockerfile must ${explanation}: missing ${needle}" >&2
		exit 1
	fi
}

test -f "$dockerfile" || {
	echo "pckg Dockerfile is missing" >&2
	exit 1
}

require "COPY beskid_web_common" "copy beskid_web_common into the web build"
require "corepack prepare pnpm@10.17.1 --activate" "activate the pnpm version pinned by both web workspaces"
require "pnpm install --dir /src/beskid_web_common --frozen-lockfile" "frozen-install beskid_web_common first"
require "pnpm install --dir /src/pckg/web --frozen-lockfile" "frozen-install the pckg web client"
require "pnpm --dir /src/pckg/web run build" "build the pckg web client"
require "cargo build --release -p beskid_pckg_server" "build the Rust registry server"
require "/app/web" "serve the bundled web client from /app/web"
require "PCKG_ARTIFACT_ROOT=/app/packages" "preserve the Compose artifact-volume mount path"
require "PCKG_BIND_ADDRESS=0.0.0.0:8082" "bind the registry on port 8082"
require "EXPOSE 8082" "expose port 8082"
require "ca-certificates curl util-linux" "install curl for Compose healthchecks and setpriv for entrypoint privilege drop"
require "CMD curl -f http://127.0.0.1:8082/health/ready || exit 1" "healthcheck readiness with curl"
require 'mkdir -p \"$PCKG_ARTIFACT_ROOT\"' "create the mounted artifact directory as root"
require 'chown pckg:pckg \"$PCKG_ARTIFACT_ROOT\"' "grant the mounted artifact directory to pckg"
require "exec setpriv --reuid=pckg --regid=pckg --init-groups /app/beskid_pckg_server" "drop to pckg before starting the server"
