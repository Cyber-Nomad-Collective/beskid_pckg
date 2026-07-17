#!/usr/bin/env bash
# Read-only preflight for a pckg C# -> Rust data-cutover rehearsal.
#
# Run this only against a restored production clone, never the live database.
# It intentionally does not import data or mutate the database.  The Rust
# importer remains an explicit operator action because it requires reviewed
# legacy-Identity -> Auth Hub GitHub-subject mappings.
set -euo pipefail

: "${PCKG_LEGACY_DATABASE_URL:?set a PostgreSQL URL for a restored legacy database clone}"

psql "$PCKG_LEGACY_DATABASE_URL" -X -v ON_ERROR_STOP=1 <<'SQL'
\pset footer off
\pset null '(null)'
\echo '== Required legacy table shape =='
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name IN ('Packages', 'PackageVersions')
ORDER BY table_name;

\echo '== Legacy registry counts =='
SELECT 'packages' AS entity, COUNT(*) AS count FROM "Packages"
UNION ALL
SELECT 'versions', COUNT(*) FROM "PackageVersions";

\echo '== Legacy owners requiring reviewed GitHub mappings =='
SELECT "OwnerUserId" AS legacy_identity_id, COUNT(*) AS package_count
FROM "Packages"
GROUP BY "OwnerUserId"
ORDER BY "OwnerUserId";

\echo '== Mapping table status (present only after Rust migrations) =='
SELECT
  to_regclass('public.pckg_legacy_identity_subject_map') AS mapping_table,
  to_regclass('public.pckg_legacy_identity_cutover_runs') AS audit_table,
  to_regclass('public.pckg_packages') AS rust_packages_table,
  to_regclass('public.pckg_package_versions') AS rust_versions_table;

\echo '== Artifact keys referenced by legacy versions =='
SELECT "StorageKey", COUNT(*) AS version_count
FROM "PackageVersions"
GROUP BY "StorageKey"
ORDER BY "StorageKey";
SQL

echo
echo 'Preflight completed. Do not delete C# tables or artifacts yet.'
echo 'Next: have two operators review an explicit old Identity ID -> github:<numeric-id> mapping export.'
echo 'Next: run the documented pckg_cutover dry-run, then the double-gated --apply rehearsal after review.'
