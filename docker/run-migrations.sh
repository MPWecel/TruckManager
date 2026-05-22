#!/usr/bin/env bash
# run-migrations.sh — apply EF Core migrations against the configured database.
#
# Phase 1: placeholder. EF Core migrations don't exist yet — they're introduced in
# Phase 4. The real body will run `dotnet ef database update` (or invoke the
# already-built migration bundle from the API image).

set -euo pipefail

echo "run-migrations.sh: not yet implemented (Phase 4)." >&2
exit 0
