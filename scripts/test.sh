#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="$ROOT/.build"

TARGET="${1:-all}"   # all | backend | frontend

run_backend_tests() {
  echo "==> Backend: dotnet test..."
  mkdir -p "$BUILD_DIR/coverage/backend" "$BUILD_DIR/test-results/backend"
  dotnet test "$ROOT/backend/src/Api.Tests/Api.Tests.csproj" -c Release \
    --collect:"XPlat Code Coverage" \
    --results-directory "$BUILD_DIR/test-results/backend" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput="$BUILD_DIR/coverage/backend/" \
    /p:ReportTypes=Html \
    "/p:Exclude=[GroceryStore.Chatbot.Api]Api.Services.OpenAIRetrievalChatService%2C[GroceryStore.Chatbot.Api]Api.Services.OpenAIEmbeddingService"
}

run_frontend_tests() {
  echo "==> Frontend: vitest..."
  cd "$ROOT/frontend"
  npm test -- --coverage
}

case "$TARGET" in
  backend)
    run_backend_tests
    ;;
  frontend)
    run_frontend_tests
    ;;
  all)
    run_backend_tests
    echo ""
    run_frontend_tests
    ;;
  *)
    echo "Usage: $0 [all|backend|frontend]" >&2
    exit 1
    ;;
esac

echo ""
echo "All tests passed."
