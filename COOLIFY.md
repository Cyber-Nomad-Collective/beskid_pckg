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

Store the pckg service token, session secret, and database password at
`secret/beskid/production/pckg`. Auth Hub provisions the paired service token;
pckg accepts GitHub-authenticated browser sessions only through that token and
never stores a GitHub OAuth token or local password.

| Variable | Required | Notes |
|----------|----------|--------|
| `PCKG_AUTH_HUB_SERVICE_TOKEN` | yes | Paired service token issued by Auth Hub for pckg |
| `PCKG_SESSION_SECRET` | yes | Separate 32+ character pckg browser-session signing secret |
| `POSTGRES_PASSWORD` | yes | Password used in the PostgreSQL connection URL |
| `PCKG_COOKIE_SECURE` | yes in production | Keep `true` for HTTPS deployments |
| `PCKG_ADMIN_BOOTSTRAP_SUBJECT` | one-time optional | Auth Hub GitHub subject granted the initial pckg superadmin role |

Configure the Auth Hub application and its pckg service token before deploying;
see [site/auth/COOLIFY.md](../site/auth/COOLIFY.md) for the hub deployment.

## Health

`curl -f http://localhost:8082/health/ready` — expose port **8082** to the proxy (`pckg.beskid-lang.org`).

## Platform matrix

Cross-service URLs, OpenBao paths, and shared auth variables: [beskid_infra/docs/deploy-matrix.md](../beskid_infra/docs/deploy-matrix.md).
