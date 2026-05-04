#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

TARGET="${1:-all}"   # all | backend | frontend

if [[ ! -f "$ROOT/.env" ]]; then
  echo "ERROR: $ROOT/.env not found. Run scripts/setup.sh first." >&2
  exit 1
fi

build_service() {
  local service="$1"
  echo "==> Building Docker image: $service..."
  docker compose --file "$ROOT/docker-compose.yml" build "$service"
}

case "$TARGET" in
  backend)
    build_service backend
    ;;
  frontend)
    build_service frontend
    ;;
  all)
    echo "==> Building all Docker images..."
    docker compose --file "$ROOT/docker-compose.yml" build
    ;;
  *)
    echo "Usage: $0 [all|backend|frontend]" >&2
    exit 1
    ;;
esac

echo ""
echo "Build complete. Run 'docker compose up -d' to start the stack."
