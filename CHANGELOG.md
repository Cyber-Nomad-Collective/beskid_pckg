# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Changed

- Replace the retired .NET container with a root-context Rust registry image that bundles the pckg web client, preserves the `/app/packages` Compose volume contract, and drops to `pckg` after root-only volume initialization.

### Fixed

- Serve Auth Hub pairing status and approval endpoints under the configured single `/api` prefix.
- Publish the repository's .NET 10 Server application in the pckg container instead of a non-existent compiler Rust binary.
- Refresh the checked-in web distribution after integrating shared UI component styles.
