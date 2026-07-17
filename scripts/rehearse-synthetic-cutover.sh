#!/usr/bin/env bash
# Fully disposable pckg cutover proof. This never connects to a user-supplied
# database: it creates one temporary PostgreSQL container, seeds a minimal
# representation of the legacy schema, runs the real Rust runner in dry-run
# and guarded apply modes, and reconciles the durable import result.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compiler="$root/compiler"
name="pckg-cutover-synthetic-$$"
tmp="$(mktemp -d "${TMPDIR:-/tmp}/pckg-cutover-synthetic.XXXXXX")"

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  rm -rf "$tmp"
}
trap cleanup EXIT

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "missing required command: $1" >&2
    exit 1
  }
}

require docker
require cargo
require sha256sum
docker info >/dev/null 2>&1 || {
  echo "Docker Desktop must be running for the disposable synthetic rehearsal" >&2
  exit 1
}

docker run --detach --rm --name "$name" \
  -e POSTGRES_DB=pckg_rehearsal \
  -e POSTGRES_USER=pckg \
  -e POSTGRES_PASSWORD=pckg-synthetic-only \
  -p 127.0.0.1::5432 postgres:16-alpine >/dev/null

for _ in $(seq 1 30); do
  if docker exec "$name" pg_isready -U pckg -d pckg_rehearsal >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
docker exec "$name" pg_isready -U pckg -d pckg_rehearsal >/dev/null

port="$(docker port "$name" 5432/tcp | sed -n 's/.*:\([0-9][0-9]*\)$/\1/p')"
database_url="postgres://pckg:pckg-synthetic-only@127.0.0.1:${port}/pckg_rehearsal"
artifact_root="$tmp/artifacts"
mapping_file="$tmp/mappings.tsv"
mkdir -p "$artifact_root/packages"
printf 'synthetic pckg artifact\n' >"$artifact_root/packages/demo-1.0.0.bpk"
checksum="$(sha256sum "$artifact_root/packages/demo-1.0.0.bpk" | awk '{print $1}')"

docker exec -i "$name" psql -v ON_ERROR_STOP=1 -U pckg -d pckg_rehearsal <<SQL
CREATE TABLE "Packages" (
  "Id" UUID PRIMARY KEY,
  "OwnerUserId" TEXT NOT NULL,
  "Name" TEXT NOT NULL,
  "IsPublic" BOOLEAN NOT NULL,
  "CreatedAtUtc" TIMESTAMPTZ NOT NULL,
  "UpdatedAtUtc" TIMESTAMPTZ NOT NULL
);
CREATE TABLE "PackageVersions" (
  "Id" UUID PRIMARY KEY,
  "PackageId" UUID NOT NULL REFERENCES "Packages"("Id"),
  "Version" TEXT NOT NULL,
  "ChecksumSha256" TEXT NOT NULL,
  "StorageKey" TEXT NOT NULL,
  "SizeBytes" BIGINT NOT NULL,
  "IsYanked" BOOLEAN NOT NULL,
  "PublishedAtUtc" TIMESTAMPTZ NOT NULL,
  "YankedAtUtc" TIMESTAMPTZ NULL
);
INSERT INTO "Packages" VALUES
 ('11111111-1111-4111-8111-111111111111', 'legacy-owner-1', 'Synthetic.Demo', TRUE, now(), now());
INSERT INTO "PackageVersions" VALUES
 ('22222222-2222-4222-8222-222222222222', '11111111-1111-4111-8111-111111111111', '1.0.0', '$checksum', 'packages/demo-1.0.0.bpk', $(wc -c <"$artifact_root/packages/demo-1.0.0.bpk"), FALSE, now(), NULL);
SQL

printf 'legacy-owner-1\tgithub:4242\tsynthetic-reviewer\t1760000000\n' >"$mapping_file"
run_id='33333333-3333-4333-8333-333333333333'
base=(cargo run -q -p beskid_pckg_store --bin pckg_cutover --
  --database-url "$database_url"
  --mapping-file "$mapping_file"
  --artifact-root "$artifact_root"
  --requested-by synthetic-operator
  --run-id "$run_id")

echo '== dry run =='
(cd "$compiler" && "${base[@]}")
dry_tables="$(docker exec "$name" psql -At -U pckg -d pckg_rehearsal -c "SELECT to_regclass('public.pckg_packages') IS NULL")"
test "$dry_tables" = 't'

echo '== guarded apply =='
(cd "$compiler" && PCKG_CUTOVER_REHEARSAL=restored-clone "${base[@]}" --apply)

echo '== durable reconciliation =='
docker exec -i "$name" psql -v ON_ERROR_STOP=1 -U pckg -d pckg_rehearsal <<SQL
DO \\$\$
DECLARE
  package_count BIGINT;
  version_count BIGINT;
  audit_status TEXT;
  audit_packages BIGINT;
  audit_versions BIGINT;
  artifact_checksum TEXT;
BEGIN
  SELECT COUNT(*) INTO package_count FROM pckg_packages;
  SELECT COUNT(*) INTO version_count FROM pckg_package_versions;
  SELECT status, imported_package_count, imported_version_count
    INTO audit_status, audit_packages, audit_versions
    FROM pckg_legacy_identity_cutover_runs
   WHERE run_id = '$run_id';
  SELECT checksum_sha256 INTO artifact_checksum FROM pckg_package_versions
   WHERE storage_key = 'packages/demo-1.0.0.bpk';
  IF package_count <> 1 OR version_count <> 1
     OR audit_status <> 'completed' OR audit_packages <> 1 OR audit_versions <> 1
     OR artifact_checksum <> '$checksum' THEN
    RAISE EXCEPTION 'synthetic reconciliation failed: packages %, versions %, status %, report %/%, checksum %',
      package_count, version_count, audit_status, audit_packages, audit_versions, artifact_checksum;
  END IF;
END \\$\$;
SQL
echo 'Synthetic pckg cutover rehearsal passed: dry run was non-mutating and guarded apply reconciled 1 package, 1 version, and 1 artifact checksum.'
