#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

FIX=false
if [[ "${1:-}" == "--fix" ]]; then
  FIX=true
fi

echo "==> Backend: dotnet format..."
if $FIX; then
  dotnet format "$ROOT/backend/AIEngineerCodingChallenge.Backend.slnx"
else
  dotnet format "$ROOT/backend/AIEngineerCodingChallenge.Backend.slnx" --verify-no-changes
fi

echo ""
echo "==> Frontend: ESLint..."
cd "$ROOT/frontend"
if $FIX; then
  npx eslint . --fix
else
  npm run lint
fi

echo ""
echo "==> Frontend: Prettier..."
if $FIX; then
  npm run format
else
  npx prettier --check .
fi

echo ""
echo "==> E2E: Prettier..."
if $FIX; then
  "$ROOT/frontend/node_modules/.bin/prettier" --write "$ROOT/e2e/playwright.config.ts" "$ROOT/e2e/playwright.evidence.config.ts" "$ROOT/e2e/tests"
else
  "$ROOT/frontend/node_modules/.bin/prettier" --check "$ROOT/e2e/playwright.config.ts" "$ROOT/e2e/playwright.evidence.config.ts" "$ROOT/e2e/tests"
fi

echo ""
echo "Format/lint checks passed."
