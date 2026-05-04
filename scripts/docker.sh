#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE="docker compose --file $ROOT/docker-compose.yml"

CMD="${1:-}"
SERVICE="${2:-}"   # optional: backend | frontend

usage() {
  echo "Usage: $0 <up|down|restart|logs|status> [backend|frontend]" >&2
  exit 1
}

case "$CMD" in
  up)
    if [[ ! -f "$ROOT/.env" ]]; then
      echo "ERROR: $ROOT/.env not found. Run scripts/setup.sh first." >&2
      exit 1
    fi
    echo "==> Starting stack${SERVICE:+ ($SERVICE)}..."
    $COMPOSE up -d --build ${SERVICE}
    echo ""
    echo "Backend  → http://localhost:${PORT:-5181}"
    echo "Frontend → http://localhost:5173"
    ;;
  down)
    echo "==> Stopping stack${SERVICE:+ ($SERVICE)}..."
    $COMPOSE down ${SERVICE}
    ;;
  restart)
    echo "==> Restarting stack${SERVICE:+ ($SERVICE)}..."
    $COMPOSE down ${SERVICE}
    $COMPOSE up -d --build ${SERVICE}
    ;;
  logs)
    $COMPOSE logs -f ${SERVICE}
    ;;
  status)
    $COMPOSE ps ${SERVICE}
    ;;
  *)
    usage
    ;;
esac
