FROM oven/bun:1.3.14 AS web-build
WORKDIR /src/pckg/web
COPY pckg/web/package.json ./
COPY pckg/web/.npmrc ./
ARG NODE_AUTH_TOKEN
ENV NODE_AUTH_TOKEN=${NODE_AUTH_TOKEN}
RUN bun install --frozen-lockfile || bun install
COPY pckg/web/ ./
RUN bun run build

FROM rust:1.88-bookworm AS server-build
WORKDIR /src
COPY compiler/ ./compiler/
WORKDIR /src/compiler
RUN cargo build --release -p beskid_pckg_server

FROM debian:bookworm-slim AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl util-linux \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --uid 10001 pckg \
    && mkdir -p /app/web /app/packages /app/data \
    && chown -R pckg:pckg /app
COPY --from=server-build /src/compiler/target/release/beskid_pckg_server /usr/local/bin/beskid_pckg_server
COPY --from=web-build /src/pckg/web/dist /app/web
ENV PCKG_WEB_ROOT=/app/web \
    PCKG_ARTIFACT_ROOT=/app/packages \
    PCKG_COOKIE_SECURE=true \
    PCKG_BIND_ADDR=0.0.0.0:8082
EXPOSE 8082
# Docker creates named volumes as root. Normalize the writable mounts before
# dropping privileges so both fresh and restored artifact volumes are writable.
ENTRYPOINT ["/bin/sh", "-ec", "mkdir -p \"$PCKG_ARTIFACT_ROOT\" /app/data && chown -R pckg:pckg \"$PCKG_ARTIFACT_ROOT\" /app/data && exec setpriv --reuid=10001 --regid=10001 --init-groups beskid_pckg_server"]
