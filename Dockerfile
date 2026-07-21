FROM oven/bun:1.3.14 AS web-build
# Context is the superrepo root so file:../../beskid_web_common resolves.
WORKDIR /src/pckg/web
COPY pckg/web/package.json pckg/web/bun.lock pckg/web/.npmrc ./
COPY beskid_web_common /src/beskid_web_common
ARG NODE_AUTH_TOKEN
ENV NODE_AUTH_TOKEN=${NODE_AUTH_TOKEN}
RUN bun install --frozen-lockfile
COPY pckg/web/ ./
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
