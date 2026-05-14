# Testing Guide

## Test Layers

- Backend unit/integration-style tests: `backend/src/Api.Tests`
- Frontend unit/component tests: `frontend/src/**/*.test.ts(x)`
- End-to-end tests: `e2e/tests`

## Run Tests

### Backend

- Bash: `./scripts/test.sh backend`
- PowerShell: `./scripts/test.ps1 backend`
- Direct: `dotnet test backend/src/Api.Tests/Api.Tests.csproj`

### Frontend

- Bash: `./scripts/test.sh frontend`
- PowerShell: `./scripts/test.ps1 frontend`
- Direct: `npm test --prefix frontend`
- With coverage: `npm test --prefix frontend -- --coverage`

### E2E (Playwright)

- Bash: `./scripts/e2e.sh test`
- PowerShell: `./scripts/e2e.ps1 test`
- Direct: `npx playwright test` from `e2e/`

## Evidence Workflow

Use evidence mode to generate screenshots and reports for reviewers:

- Bash: `./scripts/e2e.sh evidence`
- PowerShell: `./scripts/e2e.ps1 evidence`

Output locations:

- Human-readable evidence: `evidences/evidence.md`
- Raw artifacts: `evidences/raw/`
- Playwright report: `e2e/playwright-report/`

## Coverage Expectations

- Backend test runs collect coverage artifacts (Cobertura).
- Frontend CI runs coverage with thresholds on core files:
  - `src/pages/ChatPage.tsx`
  - `src/components/ChatComposer.tsx`
  - `src/components/CitationsPanel.tsx`
  - `src/services/apiClient.ts`

## Troubleshooting

- If frontend tests fail with async timing issues, rerun with:
  - `npm test --prefix frontend -- --runInBand` (if needed in local debugging)
- If e2e fails only in CI, inspect uploaded Playwright traces and screenshots.
- If Dockerized runs fail, validate compose first:
  - `docker compose config`
