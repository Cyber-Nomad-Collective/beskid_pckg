# Coolify: pckg registry

Application: **pckg** — app image `ghcr.io/cyber-nomad-collective/beskid-pckg:${IMAGE_TAG}` plus **Postgres** in the same compose stack.

| Environment | Branch | Image tag |
|-------------|--------|-----------|
| production | `main` | `main` |
| staging | `staging` | `staging` |

## Compose entry

**Coolify:** [`docker-compose.coolify.yml`](docker-compose.coolify.yml) — use a **separate Postgres volume** per environment (never share production DB with staging).

**Local build:** [`docker-compose.yml`](docker-compose.yml)

## Build

- **Drone CI** (`.drone.yml`) builds and pushes the app image.
- Coolify must not run server-side .NET builds after cutover.

## Runtime secrets

Inject via OpenBao → [beskid_infra](https://github.com/Cyber-Nomad-Collective/beskid_infra) OpenTofu (`secret/beskid/{environment}/pckg`). See superrepo [site/auth/COOLIFY.md](../site/auth/COOLIFY.md) for auth hub pairing.

## Health

`curl -f http://localhost:8082/health/ready` — expose port **8082** to the proxy.
