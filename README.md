# beskid_pckg

The Beskid package registry is a Rust HTTP service with a React client. It
stores registry data in PostgreSQL, keeps validated package artifacts on the
configured artifact volume, and delegates all browser identity to
[Auth Hub](../site/auth/README.md).

## Runtime

- Server: `beskid_pckg_server` in [`compiler/`](../compiler/)
- Client: React/Vite in [`web/`](web/), built with Bun and shared
  `@beskid/*` UI packages
- Persistence: PostgreSQL plus the `pckg_packages` artifact volume
- Identity: GitHub-only Auth Hub session handoff

The legacy C# application is retained solely as the migration source while the
transactional importer reaches complete data coverage. It is not an
operational runtime and must not be started for local development or
deployment. See [`CUTOVER.md`](CUTOVER.md) for the required reconciliation
procedure.

## Local Compose

From `pckg/`, copy the environment template and set the two Auth Hub secrets:

```bash
cp .env.example .env
# Set PCKG_AUTH_HUB_SERVICE_TOKEN and PCKG_SESSION_SECRET in .env.
./run-podman.sh up
```

This starts PostgreSQL on `5432` and the registry on
`http://localhost:8082`. The Rust service applies its SQL migrations on
startup.

Useful lifecycle commands:

```bash
./run-podman.sh logs
./run-podman.sh ps
./run-podman.sh down
./run-podman.sh down --reset
```

`--reset` removes the PostgreSQL and artifact volumes for a clean local boot.

## Local development

Build or test the React client:

```bash
bun --cwd web run test
bun --cwd web run typecheck
bun --cwd web run build
```

Build or test the Rust service from the repository root:

```bash
cd compiler
cargo test -p beskid_pckg_server
cargo run -p beskid_pckg_server
```

The service requires `PCKG_AUTH_HUB_SERVICE_TOKEN` and
`PCKG_SESSION_SECRET`; supply `PCKG_DATABASE_URL` to use PostgreSQL outside
Compose. See [`.env.example`](.env.example) for the complete local runtime
configuration.

## Identity and deliberate retirements

Browser sign-in is GitHub application login through Auth Hub only. pckg no
longer operates local Identity users, passwords, registration, bearer-token
sign-in, email/SMTP delivery, reCAPTCHA, or profile-avatar uploads. Profiles
use the GitHub identity and avatar URL supplied by Auth Hub; browser
notifications are shown in the registry UI.

## Troubleshooting

- **The service refuses to start:** set distinct values for
  `PCKG_AUTH_HUB_SERVICE_TOKEN` and `PCKG_SESSION_SECRET`.
- **Stale local state:** run `./run-podman.sh down --reset`, then
  `./run-podman.sh up`.
- **Database connection failures:** confirm the Postgres service is healthy
  with `./run-podman.sh ps` and that `PCKG_DATABASE_URL` has URL-safe
  credentials when overriding the Compose defaults.
