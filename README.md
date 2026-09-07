# beskid_pckg

The Beskid package registry is a Rust HTTP service with a React client. It
stores registry data in PostgreSQL, keeps validated package artifacts on the
configured artifact volume, and delegates all browser identity to
[Auth Hub](../site/auth/README.md).

## Runtime

- Server: `beskid_pckg_server` in [`compiler/crates/beskid_pckg_server/`](../compiler/crates/beskid_pckg_server/)
- Client: React/Vite in [`web/`](web/), built with pnpm and shared
  `@beskid/*` UI packages
- Persistence: PostgreSQL plus the `pckg_packages` artifact volume
- Identity: GitHub-only Auth Hub session handoff

The legacy .NET registry is disposable and has been retired. This Rust-backed
registry starts as a fresh store for corelib, templates, and future packages:
do not import legacy registry rows or artifacts. The Rust service is the sole
runtime, and no cutover procedure is supported from `pckg/`.

## Local Compose

The local Compose stack (`docker-compose.yml`, `run-podman.sh`) was removed
with the legacy backend. Build the Rust registry image from the repository
root so it can include both `beskid_web_common` and `compiler`:

```bash
docker build -f pckg/Dockerfile -t beskid-pckg .
```

The image preserves the established Compose contract: it listens on `8082`,
serves the bundled client from `/app/web`, and uses the mountable
`/app/packages` fresh-store artifact root. Its root entrypoint only creates
and assigns that mounted directory, then starts the server as the `pckg`
user. Set `PCKG_DATABASE_URL` and the Auth Hub environment before production
use.

## Local development

Build or test the React client:

```bash
pnpm --dir web run test
pnpm --dir web run typecheck
pnpm --dir web run build
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
- **Database connection failures:** confirm Postgres is reachable and that
  `PCKG_DATABASE_URL` has URL-safe credentials.
