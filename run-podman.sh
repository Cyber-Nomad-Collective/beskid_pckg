#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="docker-compose.yml"
RESET_VOLUMES=0
ACTION="up"
PODMAN_SOCK="/run/user/$(id -u)/podman/podman.sock"
LOCAL_DOCKER_CONFIG_DIR="${PWD}/.podman-docker-config"

while (($#)); do
  case "$1" in
    --reset)
      RESET_VOLUMES=1
      shift
      ;;
    up|down|logs|ps)
      ACTION="$1"
      shift
      ;;
    *)
      echo "Unknown argument: $1"
      echo "Usage: $0 [up|down|logs|ps] [--reset]"
      exit 2
      ;;
  esac
done

ensure_podman_socket() {
  export DOCKER_HOST="unix://${PODMAN_SOCK}"

  if [[ -S "${PODMAN_SOCK}" ]]; then
    return
  fi

  if command -v systemctl >/dev/null 2>&1; then
    systemctl --user start podman.socket >/dev/null 2>&1 || true
  fi

  if [[ ! -S "${PODMAN_SOCK}" ]]; then
    mkdir -p "$(dirname "${PODMAN_SOCK}")"
    nohup podman system service --time=0 "unix://${PODMAN_SOCK}" >/tmp/podman-system-service.log 2>&1 &
    sleep 1
  fi

  if [[ ! -S "${PODMAN_SOCK}" ]]; then
    echo "Could not start Podman socket at ${PODMAN_SOCK}."
    echo "Try: systemctl --user start podman.socket"
    exit 1
  fi

}

ensure_local_docker_config() {
  mkdir -p "${LOCAL_DOCKER_CONFIG_DIR}"
  cat > "${LOCAL_DOCKER_CONFIG_DIR}/config.json" <<'EOF'
{
  "auths": {}
}
EOF
  export DOCKER_CONFIG="${LOCAL_DOCKER_CONFIG_DIR}"
}

ensure_podman_socket
ensure_local_docker_config

compose_cmd=(podman compose -f "${COMPOSE_FILE}")

if ! "${compose_cmd[@]}" version >/dev/null 2>&1; then
  echo "podman compose is not available."
  exit 1
fi

if [[ "${ACTION}" == "down" ]]; then
  down_args=(down)
  if [[ ${RESET_VOLUMES} -eq 1 ]]; then
    down_args+=(-v)
  fi
  "${compose_cmd[@]}" "${down_args[@]}"
  echo "Stack stopped."
  exit 0
fi

if [[ "${ACTION}" == "logs" ]]; then
  "${compose_cmd[@]}" logs -f
  exit 0
fi

if [[ "${ACTION}" == "ps" ]]; then
  "${compose_cmd[@]}" ps
  exit 0
fi

if [[ ${RESET_VOLUMES} -eq 1 ]]; then
  "${compose_cmd[@]}" down -v
fi

"${compose_cmd[@]}" up --build -d
"${compose_cmd[@]}" ps

echo "pckg stack is running. Registry: http://localhost:8082"
