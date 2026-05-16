# Data Retention Controls

This document defines local data locations, retention defaults, and cleanup commands.

## Stored data locations

- Backend vector store: `backend/src/Api/Data/vector-store.json`
- Ingestion audit records: `backend/src/Api/Data/ingestion-audit.json`
- Conversation feedback records: `backend/src/Api/Data/conversation-feedback.json`
- Backend rolling logs: `backend/src/Api/Logs/`
- E2E artifacts: `e2e/test-results/`, `e2e/playwright-report/`
- Evidence artifacts: `evidences/raw/`

## Retention defaults

Configured in backend settings under `DataRetention`:

- `LogDays`: `30`
- `AuditDays`: `30`
- `FeedbackDays`: `30`
- `UploadArtifactsDays`: `7`
- `VectorStoreDays`: `90`

These values are present in:

- `backend/src/Api/appsettings.json`
- `backend/src/Api/appsettings.Development.json`

## Local cleanup command

Run from the repository root:

```bash
./scripts/cleanup-data.sh
```

The script defaults to `DRY_RUN=1` and prints candidates without deleting files.

Useful overrides:

```bash
DRY_RUN=0 ./scripts/cleanup-data.sh
DRY_RUN=0 INCLUDE_VECTOR_STORE=1 ./scripts/cleanup-data.sh
RETENTION_LOG_DAYS=14 DRY_RUN=0 ./scripts/cleanup-data.sh
```

## Safety behavior

- Dry-run is enabled by default to avoid accidental data loss.
- Vector store deletion is disabled by default (`INCLUDE_VECTOR_STORE=0`) so active demo data is preserved unless explicitly requested.
