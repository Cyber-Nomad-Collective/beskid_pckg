# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Changed

- License the hosted registry deployment and web client under
  `AGPL-3.0-only`, declare the SPDX identifier in package and OCI metadata,
  and include the license text in the runtime image.
- Browser sign-in starts at the pckg origin's Authentik outpost and preserves
  only a same-origin dashboard return URL.

- Replace the retired .NET container with a root-context Rust registry image that bundles the pckg web client, preserves the `/app/packages` Compose volume contract, and drops to `pckg` after root-only volume initialization.
- Document seed-derived canonical `PCKG_DATABASE_URL` deployment and
  no-session bearer publication; browser management remains fail-closed
  pending trusted forward-auth.
- Align the local and Coolify Compose references with the Rust server's
  `PCKG_*` and `SHELL_*` environment contract.
- Limit the React client to Rust-backed package, publisher, review, API-key,
  and administration routes; align its session, publisher, and administrator
  response shapes with the Rust contracts. Package publication remains owned
  by the artifact-validating compiler CLI and GitHub release path.
- Route package detail actions through the authoritative `packageKind`
  discriminator: libraries expose structured documentation, templates show
  `beskid new` install commands and metadata, and tools show exact-version
  `beskid pckg download` instructions.

### Fixed

- Include the BSOL workspace in the Rust image build so artifact manifest
  validation compiles in the same fresh checkout used by GitHub delivery.
- Add an isolated consumer type contract for the shared AST graph exports so
  missing transitive declarations fail before the pckg web production build.
- Refresh the checked-in web distribution after integrating shared UI component styles.

### Removed

- Remove the retired Nox dependency manifest and Podman launcher left behind
  by the C# service, keeping the Rust container workflow as the sole local
  service launch path.
- Purge the retired C# server, integration and unit test projects, Aspire host,
  service defaults, solution/build configuration, disabled .NET CI notes, and
  obsolete database-cutover tooling. The React client and compiler-owned Rust
  registry are the only maintained pckg implementations.
- Remove C#-only onboarding, Auth Hub pairing, email settings, profile,
  notification, board, post, comment, vote, lock, and follow pages and API
  clients instead of shipping dead compatibility surfaces.
- Remove the browser package-upload route, navigation, and API method rather
  than maintain a second publisher that accepts caller-supplied identity and
  version metadata.
