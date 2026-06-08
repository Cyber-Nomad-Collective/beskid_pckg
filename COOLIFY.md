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

## Service pairing

1. Deploy and complete [auth hub](../site/auth/COOLIFY.md) onboarding.
2. Hub admin: **Admin → Service pairing → New** — app `pckg`, public URL = `PCKG_PUBLIC_URL` (e.g. `https://pckg.beskid-lang.org`).
3. Open the pairing link (or `/settings/auth/pair`). When `GITHUB_SYNC_TOKEN` or `PCKG_PAIRING_APPROVER_LOGIN` is set, the link can auto-approve. Otherwise sign in as a **SuperAdmin** and submit the code.
4. Set `PCKG_PUBLIC_URL` to the registry origin used in the hub pairing request.

| Variable | Required | Notes |
|----------|----------|--------|
| `AUTH_HUB_PUBLIC_URL` | yes | Shared auth hub URL |
| `PCKG_PUBLIC_URL` | recommended | Origin for pairing `publicUrl` |
| `GITHUB_SYNC_TOKEN` | optional | Auto-approve pairing (same PAT pattern as tracker) |
| `PCKG_PAIRING_APPROVER_LOGIN` | optional | GitHub login sent to hub when approving manually |

## Health

`curl -f http://localhost:8082/health/ready` — expose port **8082** to the proxy (`pckg.beskid-lang.org`).

## Platform matrix

Cross-service URLs, OpenBao paths, and shared auth variables: [beskid_infra/docs/deploy-matrix.md](../beskid_infra/docs/deploy-matrix.md).
