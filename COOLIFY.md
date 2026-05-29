# Coolify: pckg registry

pckg runs as **`pckg`** + **`postgres`** services in the platform compose stack (Compose profile `pckg`).

| Environment | Branch | Image tag |
|-------------|--------|-----------|
| production | `main` | `main` |
| staging | `stg` (phase 2) | `staging` |

## Compose entry

| Mode | File |
|------|------|
| **Platform stack** | [`beskid_infra/compose/production/docker-compose.yml`](../beskid_infra/compose/production/docker-compose.yml) |
| **pckg + Postgres reference** | [`docker-compose.coolify.yml`](docker-compose.coolify.yml) |
| **Local build** | [`docker-compose.yml`](docker-compose.yml) |

Enable in production: set `compose_profiles` to `pckg` in `beskid_infra/config/coolify-production.json` and seed OpenBao `secret/beskid/production/pckg`.

## Runtime secrets

OpenBao path `secret/beskid/production/pckg` (`POSTGRES_PASSWORD`, `AUTH_HUB_PUBLIC_URL`, …). See [site/auth/COOLIFY.md](../site/auth/COOLIFY.md) for auth hub pairing.

## Health

`curl -f http://localhost:8082/health/ready` — expose port **8082** to the proxy (`pckg.beskid-lang.org`).
