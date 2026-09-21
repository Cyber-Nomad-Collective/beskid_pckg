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

require "COPY beskid_sites/apps/pckg" "copy the consolidated pckg web app"
require "pnpm install --dir /src/beskid_sites --frozen-lockfile --filter beskid-pckg..." "frozen-install the consolidated site workspace"
require "pnpm --dir /src/beskid_sites/apps/pckg run build" "build the consolidated pckg web app"
require "cargo build --release -p beskid_pckg_server" "build the Rust registry server"
require "/app/web" "serve the bundled web client from /app/web"
require "PCKG_ARTIFACT_ROOT=/app/packages" "preserve the Compose artifact-volume mount path"
require "PCKG_BIND_ADDRESS=0.0.0.0:8083" "bind the registry on its internal port"
require "EXPOSE 8082" "expose port 8082"
require "ca-certificates curl util-linux" "install curl for Compose healthchecks and setpriv for entrypoint privilege drop"
require "CMD curl -f http://127.0.0.1:8082/api/health || exit 1" "healthcheck readiness with curl"
require 'mkdir -p \"$PCKG_ARTIFACT_ROOT\"' "create the mounted artifact directory as root"
require 'chown pckg:pckg \"$PCKG_ARTIFACT_ROOT\"' "grant the mounted artifact directory to pckg"
require "exec node /app/web/.output/server/index.mjs" "start the consolidated web server"
