# Reviewer Notes: Tradeoffs And Future Work

## Current Version (2026-05-18)

- Chat now supports streaming responses and richer UX patterns (retry actions, improved citation handling, and persisted session state).
- Ingestion now supports scoped knowledge bases, checksum/version traceability, duplicate detection, and incremental updates.
- Operational controls now include circuit-breaker configuration, structured confidence/usage metadata, and expanded diagnostics guidance.

Forward-looking enhancement ideas are maintained in `TODO.MD`.

## Current Tradeoffs

- JSON vector store was kept as the default for local-first portability and low setup friction.
- Fallback embedding/chat behavior remains deterministic to support no-key development and CI.
- Stateless chat API keeps backend scaling simple, with conversation state owned by the client.
- Docker configuration prioritizes local reproducibility over production orchestrator complexity.

## Known Future Work

- Pluggable vector-store providers (while preserving local JSON fallback).
- Production-grade identity integration beyond local API-key auth (OIDC/JWT and policy hardening).
- Optional malware scanning/OCR hardening for uploaded document pipelines.
- Benchmark suite for larger corpora and concurrent chat/ingest workloads.
- Expanded UX explainability for confidence scoring and follow-up suggestion controls.
