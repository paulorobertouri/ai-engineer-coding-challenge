#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_URL="${BACKEND_URL:-http://localhost:5181}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT_DIR="$ROOT/test-results/diagnostics-$TIMESTAMP"
SUMMARY_FILE="$OUT_DIR/summary.txt"

mkdir -p "$OUT_DIR"

append() {
  {
    echo ""
    echo "## $1"
  } >>"$SUMMARY_FILE"
}

run_and_capture() {
  local title="$1"
  shift
  append "$title"
  if "$@" >>"$SUMMARY_FILE" 2>&1; then
    return 0
  fi

  echo "(command failed: $*)" >>"$SUMMARY_FILE"
  return 0
}

mask_env_file() {
  local source_file="$1"
  local target_file="$2"

  if [[ ! -f "$source_file" ]]; then
    return 0
  fi

  awk -F= '
    BEGIN { OFS="=" }
    /^[[:space:]]*#/ { print $0; next }
    NF < 2 { print $0; next }
    {
      key=$1
      value=substr($0, index($0, "=") + 1)
      if (key ~ /(KEY|TOKEN|SECRET|PASSWORD|CONNECTIONSTRING)/) {
        if (length(value) > 0) {
          print key, "***REDACTED***"
        } else {
          print key, ""
        }
      } else {
        print $0
      }
    }
  ' "$source_file" >"$target_file"
}

{
  echo "SOP Assistant diagnostics"
  echo "Generated at (UTC): $TIMESTAMP"
  echo "Workspace: $ROOT"
  echo "Backend URL: $BACKEND_URL"
} >"$SUMMARY_FILE"

run_and_capture "Host environment" uname -a
run_and_capture "Git status" git -C "$ROOT" status --short
run_and_capture "Git branch" git -C "$ROOT" branch --show-current
run_and_capture "Dotnet info" dotnet --info
run_and_capture "Node version" node --version
run_and_capture "Npm version" npm --version
run_and_capture "Docker version" docker --version
run_and_capture "Docker compose version" docker compose version

append "Health endpoint"
curl -sS --max-time 8 "$BACKEND_URL/api/v1/health" >"$OUT_DIR/health.json" || echo '{"error":"health endpoint unavailable"}' >"$OUT_DIR/health.json"
cat "$OUT_DIR/health.json" >>"$SUMMARY_FILE"

append "Readiness endpoint"
curl -sS --max-time 8 "$BACKEND_URL/api/v1/ready" >"$OUT_DIR/ready.json" || echo '{"error":"ready endpoint unavailable"}' >"$OUT_DIR/ready.json"
cat "$OUT_DIR/ready.json" >>"$SUMMARY_FILE"

append "Docker compose status"
if [[ -f "$ROOT/docker-compose.yml" ]]; then
  docker compose --file "$ROOT/docker-compose.yml" ps >>"$SUMMARY_FILE" 2>&1 || true
fi

append "Recent docker logs"
if [[ -f "$ROOT/docker-compose.yml" ]]; then
  docker compose --file "$ROOT/docker-compose.yml" logs --tail 200 backend frontend >"$OUT_DIR/docker-logs.txt" 2>&1 || true
  cat "$OUT_DIR/docker-logs.txt" >>"$SUMMARY_FILE"
fi

append "Recent backend file logs"
if [[ -d "$ROOT/backend/src/Api/Logs" ]]; then
  ls -1t "$ROOT/backend/src/Api/Logs" | head -n 3 >"$OUT_DIR/backend-log-files.txt" || true
  while read -r log_file; do
    [[ -z "$log_file" ]] && continue
    {
      echo ""
      echo "### $log_file"
      tail -n 200 "$ROOT/backend/src/Api/Logs/$log_file" || true
    } >>"$OUT_DIR/backend-log-tail.txt"
  done <"$OUT_DIR/backend-log-files.txt"
fi

append "Sanitized config"
mask_env_file "$ROOT/.env" "$OUT_DIR/env.sanitized"
mask_env_file "$ROOT/frontend/.env" "$OUT_DIR/frontend-env.sanitized"
if [[ -f "$OUT_DIR/env.sanitized" ]]; then
  {
    echo ""
    echo "### .env"
    cat "$OUT_DIR/env.sanitized"
  } >>"$SUMMARY_FILE"
fi
if [[ -f "$OUT_DIR/frontend-env.sanitized" ]]; then
  {
    echo ""
    echo "### frontend/.env"
    cat "$OUT_DIR/frontend-env.sanitized"
  } >>"$SUMMARY_FILE"
fi

cat <<EOF
Diagnostics written to:
  $OUT_DIR
Main summary:
  $SUMMARY_FILE
EOF
