# pckg Rust cutover rehearsal

This procedure rehearses the one-way migration from the legacy C# registry
schema to the Rust registry on a restored database and artifact-store clone.
It never targets production and does not automatically copy data.

## Preconditions

1. Quiesce a source snapshot: legacy PostgreSQL database plus every object
   addressed by `PackageVersions.StorageKey`.
2. Restore both into isolated rehearsal infrastructure. Keep the legacy
   tables (`"Packages"`, `"PackageVersions"`) and the Rust tables in the same
   PostgreSQL database for the importer.
3. Obtain a dual-reviewed mapping export from the legacy Identity primary key
   to an Auth Hub `github:<numeric-id>` subject. Do not derive it from login,
   email, or display name.
4. Configure distinct rehearsal values for `PCKG_AUTH_HUB_SERVICE_TOKEN` and
   `PCKG_SESSION_SECRET`; never reuse production browser-session secrets.

## Read-only preflight

```bash
export PCKG_LEGACY_DATABASE_URL='postgres://.../pckg-rehearsal'
bash ./pckg/scripts/verify-cutover-rehearsal.sh
```

## Disposable local proof

The repository also contains a self-contained rehearsal for runner changes. It
uses a new temporary PostgreSQL container and a generated artifact directory;
it cannot target a supplied database URL or artifact root.

```bash
bash ./pckg/scripts/rehearse-synthetic-cutover.sh
```

It runs the actual `pckg_cutover` binary once without `--apply`, confirms that
no Rust tables were created, then runs the double-gated apply and asserts the
durable count report and imported SHA-256 artifact record. This is regression
coverage for the operator tooling, not evidence that a production snapshot has
been rehearsed.

The command fails if the expected legacy schema is absent, prints package and
version counts, lists legacy owners for the mapping review, and lists every
artifact key that must remain readable after cutover.

Validate the container configuration without starting it:

```bash
PCKG_AUTH_HUB_SERVICE_TOKEN=rehearsal-handoff-token \
PCKG_SESSION_SECRET=rehearsal-session-secret \
docker compose -f pckg/docker-compose.yml config
```

## Import gate

`SqlxPackageRepository::import_legacy_identity_cutover` is deliberately not
called at server startup. It starts an audit run, writes only reviewed mappings,
then rejects the entire import if **any** `Packages.OwnerUserId` has no mapping.
On rejection it commits the audit report but imports zero packages and versions.
On success, ownership is rewritten only to the reviewed `github:<numeric-id>`
subjects and the transaction records package/version import counts.

The current implementation has no CLI or authenticated operator endpoint for
that method. The `pckg_cutover` one-shot binary is the operator runner. It
accepts a tab-separated mapping file:

```text
# legacy_identity_id<TAB>github_subject<TAB>approved_by<TAB>approved_at_unix_seconds
legacy-id-1	github:123	security-reviewer	1760000000
```

Build and execute the default, read-only validation first:

```bash
cd compiler
cargo run -p beskid_pckg_store --bin pckg_cutover -- \
  --database-url "$PCKG_LEGACY_DATABASE_URL" \
  --mapping-file /secure/rehearsal-mappings.tsv \
  --artifact-root /restored/pckg-artifacts \
  --requested-by cutover-operator \
  --run-id "$(uuidgen | tr '[:upper:]' '[:lower:]')"
```

The command verifies every mapping, owner, artifact path and SHA-256 checksum,
then exits without changing PostgreSQL. Apply is intentionally double-gated:

```bash
PCKG_CUTOVER_REHEARSAL=restored-clone \
cargo run -p beskid_pckg_store --bin pckg_cutover -- \
  --database-url "$PCKG_LEGACY_DATABASE_URL" \
  --mapping-file /secure/rehearsal-mappings.tsv \
  --artifact-root /restored/pckg-artifacts \
  --requested-by cutover-operator \
  --run-id "$(uuidgen | tr '[:upper:]' '[:lower:]')" \
  --apply
```

`--apply` without the exact `PCKG_CUTOVER_REHEARSAL=restored-clone` marker is
refused before migrations or import. A successful run calls
`repository.migrate()`, invokes the transactional importer, and requires the
durable report counts to equal the preflight legacy package/version counts.

## Required reconciliation before C# removal

1. Verify the report status is `completed`; verify imported package/version
   counts against the preflight counts, accounting for intentional idempotent
   reruns only.
2. For every preflight `StorageKey`, validate the corresponding artifact bytes
   exist in the Rust artifact root and match the imported SHA-256 checksum.
3. Start the Rust/Bun service with PostgreSQL configured, run public package,
   private package non-disclosure, authenticated publish, API-key, and Auth Hub
   handoff smoke tests.
4. Confirm a signed handoff or session with any non-`github:<numeric-id>`
   subject is rejected. The Rust auth and server contracts cover this locally.
5. Preserve the source snapshot and cutover audit tables until rollback is no
   longer required; only then remove `pckg/src` C# source and legacy schema.
