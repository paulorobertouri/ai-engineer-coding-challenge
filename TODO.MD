# Product Improvement Backlog

This TODO focuses on next-step product value, reliability, and maintainability.

## Product Features

- [ ] Add role-aware response mode
  - Tailor answers by role (`cashier`, `manager`, `department lead`) with policy-safe scope boundaries.

- [ ] Add multilingual SOP support
  - Support language-aware retrieval and response translation with citation preservation.

- [ ] Add proactive SOP update alerts
  - Notify operators when source document versions change and require reingest/review.

- [ ] Add follow-up question suggestions
  - Generate contextual next questions from current answer + evidence coverage.

- [ ] Add policy comparison view
  - Show side-by-side diffs between SOP versions and impacted chunks/citations.

## AI Quality

- [ ] Add retrieval benchmark dashboard
  - Track fixture retrieval precision/recall trends across commits.

- [ ] Add citation faithfulness checks
  - Detect unsupported claims in generated answers before response return.

- [ ] Add answer style controls
  - Configurable answer tone/length/format while preserving grounding constraints.

- [ ] Add adaptive context budgeting
  - Dynamically tune chunk count/token budget per query complexity.

## UX Enhancements

- [ ] Add conversation export
  - Download chat transcript with cited evidence and timestamps.

- [ ] Add operator audit panel
  - Filter feedback, low-confidence answers, and failed ingests in one dashboard.

- [ ] Add source quality inspector
  - Surface chunk-size outliers, duplicate sections, and weak extraction zones.

- [ ] Add keyboard shortcut map
  - Improve power-user navigation and accessibility for chat/evidence workflows.

## Reliability And Ops

- [ ] Add ingestion retry queue controls
  - Manual retry, cancel, priority, and dead-letter visibility for background ingest jobs.

- [ ] Add readiness history endpoint
  - Keep short in-memory rolling history of readiness status transitions.

- [ ] Add chaos test profile
  - Inject controlled provider/network failures for resilience validation.

- [ ] Add per-endpoint SLO reporting
  - Track latency/error budgets and expose local SLO summaries.

## Security And Governance

- [ ] Add sensitive data redaction policy tests
  - Enforce log redaction expectations with automated test assertions.

- [ ] Add approval workflow for SOP mutations
  - Require operator review state before production knowledge-base activation.

- [ ] Add signed evidence artifacts
  - Hash/sign generated evidence assets for tamper-evident review bundles.

## Developer Experience

- [ ] Add one-command local diagnostics script
  - Collect health/readiness, config summary, and recent logs for bug reports.

- [ ] Add architecture rule tests
  - Enforce layering boundaries (controller/application/services) using architecture tests.

- [ ] Add flaky-test detector in CI
  - Re-run failing test buckets and flag instability separately from deterministic failures.

## Release Management

- [ ] Add versioned release notes
  - Keep a per-release changelog linked to ADR updates and evidence snapshots.

- [ ] Add rollback playbook
  - Document and script fallback procedures for bad deploys or broken ingests.

## Definition Of Done For New Tasks

- [ ] Formatting/lint checks pass.
- [ ] Backend/frontend/e2e tests pass.
- [ ] Build passes for backend/frontend.
- [ ] Docs and ADRs are updated when architecture or behavior changes.
- [ ] Evidence artifacts are refreshed when UI behavior is impacted.
