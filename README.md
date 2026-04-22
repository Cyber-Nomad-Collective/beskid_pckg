# beskid_pckg

Package Manager for Beskid.

## Quick Start (Compose)

From `pckg/`:

```bash
podman compose -f docker-compose.yml up --build -d
```

This starts:
- `postgres` on `5432` (database: `pckgdb`)
- `pckg` app on `http://localhost:8082`

Database migrations are applied automatically on application startup.

## Quick Start (Aspire Profile)

```bash
podman compose -f docker-compose.yml --profile aspire up --build -d
```

This additionally starts:
- `apphost` (Aspire host) on `http://localhost:18888`

## Helper Script

Use `run-podman.sh` from `pckg/`:

```bash
./run-podman.sh up
./run-podman.sh up --aspire
./run-podman.sh logs
./run-podman.sh ps
./run-podman.sh down
./run-podman.sh down --reset
```

`--reset` removes volumes for a clean database boot.

## Troubleshooting

- **`database "pckgdb" already exists`**
  - Informational in this setup; startup is idempotent.
- **Pending EF model changes**
  - Ensure the latest migration files in `src/Server/Migrations` are present.
  - Rebuild with `dotnet build src/pckg.slnx`.
- **Stale local DB state**
  - Run `./run-podman.sh down --reset`, then `./run-podman.sh up`.
