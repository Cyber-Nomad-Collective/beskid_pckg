#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="docker-compose.yml"
IMAGE_NAME="pckg:local"
CONTAINER_NAME="pckg"
HOST_PORT="8098"
CONTAINER_PORT="8082"

if podman compose version >/dev/null 2>&1; then
  if podman compose -f "${COMPOSE_FILE}" up --build -d; then
    echo "pckg compose stack is running (see ${COMPOSE_FILE})"
    exit 0
  fi

  echo "compose detected but failed; falling back to direct podman run"
fi

podman build -t "${IMAGE_NAME}" -f Dockerfile .
podman rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true

podman run -d \
  --name "${CONTAINER_NAME}" \
  -p "${HOST_PORT}:${CONTAINER_PORT}" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS="http://+:${CONTAINER_PORT}" \
  -e HttpClient__InternalBaseAddress="http://127.0.0.1:${CONTAINER_PORT}" \
  -e ConnectionStrings__DefaultConnection='Data Source=/app/data/pckg.db' \
  -v pckg_data:/app/data \
  --restart=unless-stopped \
  "${IMAGE_NAME}"

echo "started pckg with direct podman run at http://localhost:${HOST_PORT}"
