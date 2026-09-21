# syntax=docker/dockerfile:1.7
# Build with the repository root as context. The image contains the Rust
# registry and the sole TanStack pckg web application; the web app proxies
# registry API requests to the local Rust process.

FROM node:22-bookworm AS web-build

RUN corepack enable && corepack prepare pnpm@10.17.1 --activate
WORKDIR /src

COPY beskid_sites/package.json beskid_sites/pnpm-lock.yaml beskid_sites/pnpm-workspace.yaml /src/beskid_sites/
COPY beskid_sites/packages/shell-core /src/beskid_sites/packages/shell-core
COPY beskid_sites/apps/pckg /src/beskid_sites/apps/pckg
COPY beskid_web_common/packages/beskid-ui-react /src/beskid_web_common/packages/beskid-ui-react
RUN pnpm install --dir /src/beskid_sites --frozen-lockfile --filter beskid-pckg...
RUN pnpm --dir /src/beskid_sites/apps/pckg run build

FROM rust:1-bookworm AS server-build

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang mold \
    && command -v clang \
    && command -v mold \
    && mold --version \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY beskid_bsol ./beskid_bsol

COPY compiler ./compiler

WORKDIR /src/compiler
RUN cargo build --release -p beskid_pckg_server

FROM node:22-bookworm-slim AS runtime

LABEL org.opencontainers.image.licenses="AGPL-3.0-only"

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl util-linux \
    && rm -rf /var/lib/apt/lists/* \
    && addgroup --system pckg \
    && adduser --system --ingroup pckg pckg \
    && mkdir -p /app/web /app/packages \
    && chown -R pckg:pckg /app

WORKDIR /app
COPY --from=server-build --chown=pckg:pckg /src/compiler/target/release/beskid_pckg_server /app/beskid_pckg_server
COPY --from=web-build --chown=pckg:pckg /src/beskid_sites/apps/pckg/node_modules /app/web/node_modules
COPY --from=web-build --chown=pckg:pckg /src/beskid_sites/apps/pckg/package.json /app/web/package.json
COPY --from=web-build --chown=pckg:pckg /src/beskid_sites/apps/pckg/.output /app/web/.output
COPY pckg/LICENSE /usr/share/licenses/beskid-pckg/LICENSE

ENV PCKG_ARTIFACT_ROOT=/app/packages \
    PCKG_BIND_ADDRESS=0.0.0.0:8083 \
    PCKG_REGISTRY_ORIGIN=http://127.0.0.1:8083 \
    PORT=8082

EXPOSE 8082

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://127.0.0.1:8082/api/health || exit 1

ENTRYPOINT ["/bin/sh", "-ec", "mkdir -p \"$PCKG_ARTIFACT_ROOT\" && chown pckg:pckg \"$PCKG_ARTIFACT_ROOT\" && setpriv --reuid=pckg --regid=pckg --init-groups /app/beskid_pckg_server & exec node /app/web/.output/server/index.mjs"]
