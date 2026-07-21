# syntax=docker/dockerfile:1.7
FROM oven/bun:1.3.14 AS web-build
# Context is the superrepo root so file:../../beskid_web_common resolves.
WORKDIR /src
COPY beskid_web_common/package.json beskid_web_common/bun.lock ./beskid_web_common/
COPY beskid_web_common/packages ./beskid_web_common/packages
COPY pckg/web/package.json pckg/web/bun.lock pckg/web/.npmrc ./pckg/web/
ARG NODE_AUTH_TOKEN
ENV NODE_AUTH_TOKEN=${NODE_AUTH_TOKEN}
ENV BUN_INSTALL_CACHE_DIR=/bun-cache
# Install shared packages first so file: consumers resolve transitive deps and
# Vite can load source exports (graph/explorer) that are not in published 0.2.8.
RUN --mount=type=cache,target=/bun-cache bun install --cwd=/src/beskid_web_common --frozen-lockfile
RUN --mount=type=cache,target=/bun-cache bun install --cwd=/src/pckg/web --frozen-lockfile
COPY pckg/web/ ./pckg/web/
WORKDIR /src/pckg/web
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY pckg/src/Server/Server.csproj ./src/Server/
COPY pckg/src/pckg.ServiceDefaults/pckg.ServiceDefaults.csproj ./src/pckg.ServiceDefaults/
RUN dotnet restore src/Server/Server.csproj
COPY pckg/src/ ./src/
RUN dotnet publish src/Server/Server.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl util-linux \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --uid 10001 pckg \
    && mkdir -p /app/web /app/packages /app/data \
    && chown -R pckg:pckg /app
COPY --from=server-build /app/publish ./
COPY --from=web-build /src/pckg/web/dist /app/web
ENV PCKG_WEB_ROOT=/app/web \
    PCKG_ARTIFACT_ROOT=/app/packages \
    PCKG_COOKIE_SECURE=true \
    ASPNETCORE_URLS=http://+:8082
EXPOSE 8082
# Docker creates named volumes as root. Normalize the writable mounts before
# dropping privileges so both fresh and restored artifact volumes are writable.
ENTRYPOINT ["/bin/sh", "-ec", "mkdir -p \"$PCKG_ARTIFACT_ROOT\" /app/data && chown -R pckg:pckg \"$PCKG_ARTIFACT_ROOT\" /app/data && exec setpriv --reuid=10001 --regid=10001 --init-groups dotnet Server.dll"]
