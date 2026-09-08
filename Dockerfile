# syntax=docker/dockerfile:1.7
# Build with the repository root as context:
# docker build -f pckg/Dockerfile .

FROM node:22-bookworm AS web-build

RUN corepack enable && corepack prepare pnpm@10.17.1 --activate
WORKDIR /src

COPY beskid_web_common ./beskid_web_common
COPY pckg/web ./pckg/web
RUN pnpm install --dir /src/beskid_web_common --frozen-lockfile
RUN pnpm install --dir /src/pckg/web --frozen-lockfile
RUN pnpm --dir /src/pckg/web run build

FROM rust:1-bookworm AS server-build

WORKDIR /src
COPY beskid_bsol ./beskid_bsol

COPY compiler ./compiler

WORKDIR /src/compiler
RUN cargo build --release -p beskid_pckg_server

FROM debian:bookworm-slim AS runtime

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
COPY --from=web-build --chown=pckg:pckg /src/pckg/web/dist /app/web
COPY pckg/LICENSE /usr/share/licenses/beskid-pckg/LICENSE

ENV PCKG_WEB_ROOT=/app/web \
    PCKG_ARTIFACT_ROOT=/app/packages \
    PCKG_BIND_ADDRESS=0.0.0.0:8082

EXPOSE 8082

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://127.0.0.1:8082/health/ready || exit 1

ENTRYPOINT ["/bin/sh", "-ec", "mkdir -p \"$PCKG_ARTIFACT_ROOT\" && chown pckg:pckg \"$PCKG_ARTIFACT_ROOT\" && exec setpriv --reuid=pckg --regid=pckg --init-groups /app/beskid_pckg_server"]
