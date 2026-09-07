# Coolify: pckg registry

pckg runs as **`pckg`** + **`postgres`** services in the platform Compose stack
(profile `pckg`). GitHub Actions in the root repository builds the Rust-server
image, publishes it to GHCR, renders the digest-pinned Compose manifest, and
applies that manifest to Coolify.

| Environment | Deployment source | Image reference |
|-------------|-------------------|-----------------|
| staging | root `platform-delivery.yml` | immutable `sha-<root commit>` tag rendered to a digest |
| production | promoted root release | the same verified digest promoted by the delivery workflow |

## Compose entry

| Mode | File |
|------|------|
| **Platform stack** | [`beskid_infra/compose/production/docker-compose.yml`](../beskid_infra/compose/production/docker-compose.yml) |
| **pckg + Postgres reference** | [`docker-compose.coolify.yml`](docker-compose.coolify.yml) |
| **Local build** | [`docker-compose.yml`](docker-compose.yml) |

Enable in production: set `compose_profiles` to `pckg` in `beskid_infra/config/coolify-production.json` and seed OpenBao `secret/beskid/production/pckg`.

## Runtime secrets

Store the database inputs at `secret/beskid/production/pckg`. The deployment
renderer supplies one canonical URL-encoded `PCKG_DATABASE_URL`; the service
does not assemble credentials from legacy application settings.

| Variable | Required | Notes |
|----------|----------|--------|
| `POSTGRES_PASSWORD` | yes | Password used in the PostgreSQL connection URL |
| `PCKG_DATABASE_URL` | yes | Canonical URL rendered from the environment's PostgreSQL secret |
| `PCKG_ARTIFACT_ROOT` | yes | Persistent artifact volume; normally `/app/packages` |

Browser administration remains fail-closed when `SHELL_AUTH_MODE` is unset.
Enabling `authelia` requires a trusted proxy that strips client `Remote-*`
headers, completes forward authentication, and injects the verified identity.
CLI publication continues through pckg-owned bearer keys stored in PostgreSQL.

## Health

`curl -f http://localhost:8082/health/ready` — expose port **8082** to the proxy (`pckg.beskid-lang.org`).

## Platform matrix

Cross-service URLs, OpenBao paths, and shared auth variables: [beskid_infra/docs/deploy-matrix.md](../beskid_infra/docs/deploy-matrix.md).
