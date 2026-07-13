FROM oven/bun:1.3.14 AS web-build
WORKDIR /src/pckg/web
COPY pckg/web/package.json ./
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
RUN useradd --system --uid 10001 pckg \
    && mkdir -p /app/web /app/packages /app/data \
    && chown -R pckg:pckg /app
COPY --from=server-build /src/compiler/target/release/beskid_pckg_server /usr/local/bin/beskid_pckg_server
COPY --from=web-build /src/pckg/web/dist /app/web
USER pckg
ENV PCKG_WEB_ROOT=/app/web \
    PCKG_BIND_ADDR=0.0.0.0:8082
EXPOSE 8082
ENTRYPOINT ["beskid_pckg_server"]
