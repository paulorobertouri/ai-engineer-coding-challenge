# E2E Visual Baselines

This folder contains browser-based end-to-end tests and visual regression checks.

## Visual regression tests

Visual snapshots live under the Playwright snapshots folder and are versioned in git.

- Run visual tests: `CI=true npm test -- tests/visual-regression.spec.ts`
- Update visual baselines intentionally: `CI=true npx playwright test tests/visual-regression.spec.ts --update-snapshots`

Why `CI=true`:

- It forces CI behavior expected by the workflow while still running the `google-chrome` Playwright project.
- This keeps local baseline generation aligned with CI expectations.

## Deterministic test setup

Playwright starts the backend in fallback mode with deterministic local services and an isolated vector-store file at `e2e/.tmp/vector-store.json`.
This prevents stale ingested data from affecting setup/chat/citation screenshots.
