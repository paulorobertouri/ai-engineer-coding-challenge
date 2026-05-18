#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="${1:-}"
BACKUP_PATH="${2:-}"
VECTOR_STORE_PATH="$ROOT/backend/src/Api/Data/vector-store.json"

usage() {
  echo "Usage: $0 <app|ingest> [backup-file]" >&2
  exit 1
}

if [[ -z "$MODE" ]]; then
  usage
fi

case "$MODE" in
  app)
    echo "==> Rolling back app stack (restart known-good local compose state)..."
    docker compose --file "$ROOT/docker-compose.yml" down
    docker compose --file "$ROOT/docker-compose.yml" up -d --build
    echo "Rollback complete. Verify with /api/v1/health and /api/v1/ready."
    ;;
  ingest)
    if [[ -z "$BACKUP_PATH" ]]; then
      echo "ERROR: backup-file is required for ingest rollback." >&2
      usage
    fi
    if [[ ! -f "$BACKUP_PATH" ]]; then
      echo "ERROR: backup file not found: $BACKUP_PATH" >&2
      exit 1
    fi

    mkdir -p "$(dirname "$VECTOR_STORE_PATH")"
    cp "$BACKUP_PATH" "$VECTOR_STORE_PATH"
    echo "Ingest rollback complete. Restored vector store from: $BACKUP_PATH"
    echo "Target path: $VECTOR_STORE_PATH"
    ;;
  *)
    usage
    ;;
esac
