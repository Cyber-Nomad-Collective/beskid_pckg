# syntax=docker/dockerfile:1.7
FROM node:22.12.0-alpine AS web-build
RUN corepack enable && corepack prepare pnpm@10.17.1 --activate
# Context is the superrepo root so file:../../beskid_web_common resolves.
WORKDIR /src
COPY beskid_web_common ./beskid_web_common
COPY pckg/web/package.json pckg/web/pnpm-lock.yaml pckg/web/.npmrc ./pckg/web/
ARG NODE_AUTH_TOKEN
ENV NODE_AUTH_TOKEN=${NODE_AUTH_TOKEN}
# Install shared packages first so file: consumers resolve transitive deps and
# Vite can load source exports (graph/explorer) that are not in published 0.2.8.
RUN --mount=type=cache,target=/root/.local/share/pnpm/store pnpm install --dir /src/beskid_web_common --frozen-lockfile
RUN --mount=type=cache,target=/root/.local/share/pnpm/store pnpm install --dir /src/pckg/web --frozen-lockfile
COPY pckg/web/ ./pckg/web/
WORKDIR /src/pckg/web
RUN pnpm build

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
    && mkdir -p /app/web /app/packages /app/data/uploads \
    && chown -R pckg:pckg /app
# COPY after chown must re-apply ownership; otherwise uid 10001 cannot mkdir under /app.
COPY --from=server-build --chown=pckg:pckg /app/publish ./
COPY --from=web-build --chown=pckg:pckg /src/pckg/web/dist /app/web
ENV PCKG_WEB_ROOT=/app/web \
    PCKG_ARTIFACT_ROOT=/app/packages \
    PCKG_COOKIE_SECURE=true \
    ASPNETCORE_URLS=http://+:8082 \
    Storage__UploadsRootPath=/app/data/uploads
EXPOSE 8082
ENTRYPOINT ["/bin/sh", "-ec", "mkdir -p \"$PCKG_ARTIFACT_ROOT\" /app/data/uploads && chown -R pckg:pckg \"$PCKG_ARTIFACT_ROOT\" /app/data && exec setpriv --reuid=10001 --regid=10001 --init-groups dotnet Server.dll"]
