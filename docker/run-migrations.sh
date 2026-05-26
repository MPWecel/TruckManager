#!/usr/bin/env bash
# run-migrations.sh — apply pending EF Core migrations against the configured database
# WITHOUT starting the API.
#
# Normal local flow: the API auto-applies pending migrations at startup in Development
# (MigrationRunner hosted service, per Phase 4 decision #8 / ADR-0018). This script is
# the rare explicit re-run helper — useful for:
#   - Resetting a dev DB ("docker compose down -v" + this script).
#   - Applying a freshly-generated migration without restarting the API.
#   - CI-style verification of the schema against a fresh Postgres (Section H exit gate).
#
# Connection string source: same as the API — appsettings.json + appsettings.Development.json
# + ConnectionStrings__Default env var (env wins). The Default environment is Development
# unless ASPNETCORE_ENVIRONMENT is set otherwise.
#
# Usage:
#   ./docker/run-migrations.sh                                  # uses appsettings
#   ConnectionStrings__Default="Host=...;..." ./docker/run-migrations.sh

set -euo pipefail

# Resolve to the source-tree root regardless of invocation directory.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

# Restore the local dotnet-ef tool (no-op if already restored).
dotnet tool restore

dotnet ef database update \
    --project src/TruckManager.Infrastructure \
    --startup-project src/TruckManager.Api
