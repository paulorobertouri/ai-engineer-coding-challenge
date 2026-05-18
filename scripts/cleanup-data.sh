#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_DIR="$ROOT/backend/src/Api"
DATA_DIR="$API_DIR/Data"
LOGS_DIR="$API_DIR/Logs"

LOG_DAYS="${RETENTION_LOG_DAYS:-30}"
AUDIT_DAYS="${RETENTION_AUDIT_DAYS:-30}"
FEEDBACK_DAYS="${RETENTION_FEEDBACK_DAYS:-30}"
VECTOR_STORE_DAYS="${RETENTION_VECTOR_STORE_DAYS:-90}"
UPLOAD_ARTIFACT_DAYS="${RETENTION_UPLOAD_ARTIFACT_DAYS:-7}"
DRY_RUN="${DRY_RUN:-1}"
INCLUDE_VECTOR_STORE="${INCLUDE_VECTOR_STORE:-0}"

run_find_delete() {
  local target="$1"
  local age_days="$2"
  local label="$3"

  if [[ ! -e "$target" ]]; then
    echo "[skip] $label: $target (not found)"
    return
  fi

  if [[ "$DRY_RUN" == "1" ]]; then
    echo "[dry-run] $label files older than $age_days day(s):"
    find "$target" -type f -mtime "+$age_days" -print
  else
    echo "[delete] $label files older than $age_days day(s)"
    find "$target" -type f -mtime "+$age_days" -print -delete
  fi
}

echo "==> Retention cleanup started (DRY_RUN=$DRY_RUN)"
run_find_delete "$LOGS_DIR" "$LOG_DAYS" "API logs"
run_find_delete "$DATA_DIR/ingestion-audit.json" "$AUDIT_DAYS" "Ingestion audit"
run_find_delete "$DATA_DIR/conversation-feedback.json" "$FEEDBACK_DAYS" "Conversation feedback"
run_find_delete "$ROOT/evidences/raw" "$UPLOAD_ARTIFACT_DAYS" "Evidence raw artifacts"
run_find_delete "$ROOT/.build/test-results" "$UPLOAD_ARTIFACT_DAYS" "Test results"
run_find_delete "$ROOT/.build/reports" "$UPLOAD_ARTIFACT_DAYS" "Generated reports"
run_find_delete "$ROOT/.build/coverage" "$UPLOAD_ARTIFACT_DAYS" "Coverage reports"

if [[ "$INCLUDE_VECTOR_STORE" == "1" ]]; then
  run_find_delete "$DATA_DIR/vector-store.json" "$VECTOR_STORE_DAYS" "Vector store"
else
  echo "[skip] Vector store cleanup disabled (set INCLUDE_VECTOR_STORE=1 to enable)."
fi

echo "==> Retention cleanup finished"
