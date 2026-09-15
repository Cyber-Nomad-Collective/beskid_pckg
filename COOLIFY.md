# Production: pckg registry

pckg runs as **`pckg`** plus **`pckg-postgresql`** in the standalone production
Compose stack. The root AppVeyor `linux-platform` lane builds the Rust-server
image and publishes immutable `sha-*` plus controlled `production` tags to
`cr.beskid-lang.org/beskid/pckg`. Watchtower alone reconciles production.

| Environment | Deployment source | Image reference |
|-------------|-------------------|-----------------|
| production | Watchtower | `cr.beskid-lang.org/beskid/pckg:production`; retain the matching immutable `sha-*` tag as evidence |

## Compose entry

| Mode | File |
|------|------|
| **Platform stack** | [`../beskid_sites/deploy/docker-compose.yml`](../beskid_sites/deploy/docker-compose.yml) |
| **pckg + Postgres reference** | [`docker-compose.coolify.yml`](docker-compose.coolify.yml) |
| **Local build** | [`docker-compose.yml`](docker-compose.yml) |

Seed OpenBao `secret/beskid/production/pckg`, then use the production deployment script to materialize the runtime environment.

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

Cross-service runtime and rollback guidance: [beskid_sites/deploy/README.md](../beskid_sites/deploy/README.md).
