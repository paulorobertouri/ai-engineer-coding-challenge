#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPENAPI_OUT="$ROOT/frontend/src/generated/openapi.v1.json"
TYPES_OUT="$ROOT/frontend/src/generated/api-types.ts"
API_URL="http://127.0.0.1:5181"
SWAGGER_URL="$API_URL/swagger/v1/swagger.json"

mkdir -p "$(dirname "$OPENAPI_OUT")"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet is required." >&2
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "ERROR: npm is required." >&2
  exit 1
fi

echo "==> Building backend for OpenAPI generation..."
dotnet build "$ROOT/backend/src/Api/Api.csproj" -c Debug >/dev/null

echo "==> Starting backend to fetch OpenAPI schema..."
BACKEND_LOG="$(mktemp)"
DOTNET_ENVIRONMENT=Development \
  dotnet run --project "$ROOT/backend/src/Api/Api.csproj" --no-build --no-launch-profile --urls "$API_URL" \
  >"$BACKEND_LOG" 2>&1 &
BACKEND_PID=$!

cleanup() {
  if kill -0 "$BACKEND_PID" >/dev/null 2>&1; then
    kill "$BACKEND_PID" >/dev/null 2>&1 || true
    wait "$BACKEND_PID" >/dev/null 2>&1 || true
  fi
  rm -f "$BACKEND_LOG"
}
trap cleanup EXIT

echo "==> Waiting for backend startup..."
for _ in {1..40}; do
  if curl --silent --fail "$SWAGGER_URL" >"$OPENAPI_OUT"; then
    break
  fi
  sleep 0.5
done

if [[ ! -s "$OPENAPI_OUT" ]]; then
  echo "ERROR: Failed to download OpenAPI spec from $SWAGGER_URL" >&2
  exit 1
fi

echo "==> Generating TypeScript types from OpenAPI..."
cd "$ROOT/frontend"
npx swagger-typescript-api generate \
  --path "src/generated/openapi.v1.json" \
  --output "src/generated" \
  --name "api-types.ts" \
  --no-client \
  --extract-request-body \
  --extract-response-body \
  --extract-request-params

echo "==> Formatting generated API artifacts..."
npx prettier --write "src/generated/openapi.v1.json" "src/generated/api-types.ts" >/dev/null

echo "==> API type generation complete."
echo "OpenAPI: $OPENAPI_OUT"
echo "Types:   $TYPES_OUT"
