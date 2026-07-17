#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dockerfile="${root}/Dockerfile"

server_stage="$(sed -n '/^FROM rust:.* AS server-build$/,/^FROM debian:/p' "${dockerfile}")"
if [[ "${server_stage}" != *'COPY beskid_bsol/ ./beskid_bsol/'* ]]; then
    echo "pckg server image must copy the beskid_bsol workspace before building the compiler package" >&2
    exit 1
fi
