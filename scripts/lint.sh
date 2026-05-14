#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

TARGET="${1:-all}"   # all | backend | frontend

run_backend_lint() {
  echo "==> Backend: no dedicated lint step configured (skipping)."
}

run_frontend_lint() {
  echo "==> Frontend: ESLint..."
  cd "$ROOT/frontend"
  npm run lint
}

case "$TARGET" in
  backend)
    run_backend_lint
    ;;
  frontend)
    run_frontend_lint
    ;;
  all)
    run_backend_lint
    echo ""
    run_frontend_lint
    ;;
  *)
    echo "Usage: $0 [all|backend|frontend]" >&2
    exit 1
    ;;
esac

echo ""
echo "Lint checks passed."
