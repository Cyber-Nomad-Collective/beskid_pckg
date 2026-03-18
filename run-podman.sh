#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="docker-compose.yml"
IMAGE_NAME="pckg:local"
CONTAINER_NAME="pckg"
HOST_PORT="8098"

if podman compose version >/dev/null 2>&1; then
  podman compose -f "${COMPOSE_FILE}" up --build -d
  echo "pckg compose stack is running (see ${COMPOSE_FILE})"
  exit 0
fi

podman build -t "${IMAGE_NAME}" -f Dockerfile .
podman rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true

podman run -d \
  --name "${CONTAINER_NAME}" \
  -p "${HOST_PORT}:8080" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection='Data Source=/app/data/pckg.db' \
  -v pckg_data:/app/data \
  --restart=unless-stopped \
  "${IMAGE_NAME}"

echo "compose provider not found; started pckg with direct podman run at http://localhost:${HOST_PORT}"
