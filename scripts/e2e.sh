#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
E2E_DIR="$ROOT/e2e"

MODE="${1:-test}"   # test | evidence

if [[ ! -d "$E2E_DIR/node_modules" ]]; then
  echo "==> Installing e2e dependencies..."
  npm ci --prefix "$E2E_DIR"
fi

case "$MODE" in
  test)
    echo "==> Running e2e tests..."
    cd "$E2E_DIR"
    npx playwright test
    ;;
  evidence)
    echo "==> Generating evidence (screenshots)..."
    cd "$E2E_DIR"
    npx playwright test --config=playwright.evidence.config.ts tests/evidence.spec.ts
    echo ""
    echo "Artifacts saved in evidences/raw/"
    ;;
  *)
    echo "Usage: $0 [test|evidence]" >&2
    exit 1
    ;;
esac
