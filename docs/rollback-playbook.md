# Rollback Playbook

This playbook covers fast rollback for local and containerized deployments when a bad deploy or ingestion regression is detected.

## Triggers

- API health/readiness fails after deployment.
- Critical chat regression (timeouts, malformed responses, or citation failures).
- Ingest pipeline corruption or invalid vector-store content.

## Preconditions

- Access to repository and deployment host.
- Latest diagnostics bundle (run `scripts/diagnostics.sh` or `scripts/diagnostics.ps1`).
- Confirmed target rollback scope: `app` or `ingest`.

## A. App Rollback (Container Stack)

1. Capture current diagnostics:
   - `./scripts/diagnostics.sh`
2. Stop and restart stack from known-good source:
   - `./scripts/rollback.sh app`
3. Verify:
   - `curl http://localhost:5181/api/v1/health`
   - `curl http://localhost:5181/api/v1/ready`

PowerShell:
- `.\scripts\rollback.ps1 app`

## B. Ingest Rollback (Vector Store)

Use this when ingest output is invalid and you need to restore from a backup artifact.

1. Ensure backend is stopped or ingestion is paused.
2. Restore backup file:
   - `./scripts/rollback.sh ingest /absolute/path/to/vector-store-backup.json`
3. Start stack and re-verify readiness.

PowerShell:
- `.\scripts\rollback.ps1 ingest C:\path\to\vector-store-backup.json`

## Post-Rollback Verification

- Health and readiness endpoints are `200`.
- Chat endpoint returns valid response and citations for at least one known SOP question.
- No repeated high-severity errors in recent backend logs.

## Incident Follow-up

- Create/append release note in [docs/releases/README.md](./releases/README.md).
- Attach diagnostics and root-cause summary to issue/PR.
- Add regression test(s) before re-release.
