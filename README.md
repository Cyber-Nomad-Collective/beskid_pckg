# beskid_pckg

The Beskid package registry is a Rust HTTP service with a React client. It
stores registry data in PostgreSQL, keeps validated package artifacts on the
configured artifact volume, and exposes API-key publication independently of
browser-session authentication.

## Runtime

- Server: `beskid_pckg_server` in [`compiler/crates/beskid_pckg_server/`](../compiler/crates/beskid_pckg_server/)
- Client: React/Vite in [`web/`](web/), built with pnpm and shared
  `@beskid/*` UI packages
- Persistence: PostgreSQL plus the `pckg_packages` artifact volume
- Publication: active pckg bearer API keys; browser session routes require a
  separately deployed, trusted forward-auth boundary

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
user. Set the canonical `PCKG_DATABASE_URL` before production use. The OpenBao
seed path derives it from one PostgreSQL configuration source with URL-encoded
components; Compose does not construct it from password fragments.

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

Supply `PCKG_DATABASE_URL` to use PostgreSQL. `SHELL_AUTH_MODE=mock` is for
local development only. Production deliberately leaves `SHELL_AUTH_MODE`
unset until its proxy strips client `Remote-*` headers, validates requests
through forward-auth, and preserves bearer authorization. In that state,
browser-session/admin routes reject requests while active bearer API keys can
publish packages. See [`.env.example`](.env.example) for local runtime values.

## Identity and deliberate retirements

pckg does not create browser sessions or operate local Identity users,
passwords, registration, email/SMTP delivery, reCAPTCHA, or profile-avatar
uploads. A future browser management surface must be behind the verified
forward-auth boundary described above; it must never trust client-provided
identity headers directly.

## Troubleshooting

- **Database connection failures:** confirm Postgres is reachable and rerun
  the OpenBao seed path after changing any PostgreSQL user, password, database,
  host, or port value.
