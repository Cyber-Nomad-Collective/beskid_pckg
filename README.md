# beskid_pckg

The Beskid package registry is a Rust HTTP service with a React client. It
stores registry data in PostgreSQL, keeps validated package artifacts on the
configured artifact volume, and exposes API-key publication independently of
browser-session authentication.

## Runtime

- Server: `beskid_pckg_server` in [`compiler/crates/beskid_pckg_server/`](../compiler/crates/beskid_pckg_server/)
- Client: TanStack Start in [`beskid_sites/apps/pckg/`](../beskid_sites/apps/pckg/),
  built with pnpm and shared shell/UI packages
- Persistence: PostgreSQL plus the `pckg_packages` artifact volume
- Publication: active pckg bearer API keys; browser session routes require a
  separately deployed, trusted forward-auth boundary

The Rust service is the sole registry runtime. It starts from a fresh store for
corelib, templates, and future packages; no legacy backend, migration utility,
or compatibility publication path is shipped from `pckg/`.

## Local Compose

Build the Rust registry image from the repository root so it can include both
`beskid_web_common` and `compiler`:

```bash
docker build -f pckg/Dockerfile -t beskid-pckg .
```

For a disposable local registry and PostgreSQL instance, copy `.env.example`
to `.env` and run `docker compose -f pckg/docker-compose.yml up --build` from
the repository root. The local reference stack uses `SHELL_AUTH_MODE=mock`;
never use mock authentication in a deployed environment.

The image preserves the established Compose contract: it listens on `8082`,
serves the bundled client from `/app/web`, proxies registry requests to the
internal Rust listener on `8083`, and uses the mountable
`/app/packages` fresh-store artifact root. Its root entrypoint only creates
and assigns that mounted directory, then starts the server as the `pckg`
user. Set the canonical `PCKG_DATABASE_URL` before production use. The OpenBao
seed path derives it from one PostgreSQL configuration source with URL-encoded
components; Compose does not construct it from password fragments.

## NodeBB community linking

Every public package owns one discussion thread (NodeBB topic) in the
"Packages" category of the community site. The registry opens the thread when
the package is created, posts a reply for every published version, stores the
topic id and slug, and serves the public URL plus live reply/view counters to
the web client. Configure all four variables or none; a partial set is a
startup error:

| Variable | Meaning |
| --- | --- |
| `PCKG_NODEBB_API_URL` | NodeBB origin for server-to-server Write and Read API calls; in production the internal `http://beskid-community:4567` on the shared edge network |
| `PCKG_NODEBB_BOT_TOKEN` | User-bound Write API token of the bot user that posts (NodeBB Settings > API Access; not a master token) |
| `PCKG_NODEBB_PACKAGES_CID` | The "Packages" category every package thread is opened in |
| `PCKG_COMMUNITY_URL` | Public NodeBB origin used in links |

NodeBB failures never block package creation or publication; they are
recorded as `community_sync_failed` registry activity. A post that NodeBB
accepts into its moderation queue (HTTP 202) is treated the same way, so
exempt the bot user from the post queue and posting delays. Missing threads
are opened by a super administrator with
`POST /api/admin/community/reconcile`. Bot writes are serialized (NodeBB
rejects concurrent posts of one user) and thread counters plus the newest
posts are read server-side from `GET /api/topic/{tid}` with a one-minute
cache; browsers never call NodeBB.

## Local development

Build or test the web client:

```bash
pnpm --dir ../beskid_sites/apps/pckg run test
pnpm --dir ../beskid_sites/apps/pckg run typecheck
pnpm --dir ../beskid_sites/apps/pckg run build
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

## License

The pckg service deployment and web client in this repository are licensed
under the [GNU Affero General Public License v3.0 only](LICENSE). The Rust
registry backend is maintained in the separate `beskid_compiler` repository
and follows that repository's component-level licensing declarations.

Third-party dependencies and components remain under their respective
licenses.
