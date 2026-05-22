#!/usr/bin/env bash
# wait-for-db.sh — block until Postgres is accepting connections.
#
# Phase 1: placeholder. The compose stack already gates `api` on Postgres readiness
# via the `service_healthy` condition in docker-compose.yml, so this script is not
# currently invoked. The real body lands in Phase 4 when migrations are wired in
# and a startup script needs to wait outside the compose dependency graph.
#
# Usage (planned): wait-for-db.sh <host> <port> [timeout_seconds]

set -euo pipefail

echo "wait-for-db.sh: not yet implemented (Phase 4)." >&2
exit 0
