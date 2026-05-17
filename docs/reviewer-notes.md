# Reviewer Notes: Tradeoffs And Future Work

## Current Version (2026-05-17)

- Liveness/readiness checks are now explicitly separated for operational clarity.
- Readiness includes optional provider connectivity probing that is disabled by default and only relevant in OpenAI mode.
- Engineering standards are documented for backend/frontend boundaries, logging levels, and local observability defaults.

Forward-looking enhancement ideas are maintained in `TODO.MD`.

## Current Tradeoffs

- JSON vector store was kept as the default for local-first portability and low setup friction.
- Fallback embedding/chat behavior remains deterministic to support no-key development and CI.
- Stateless chat API keeps backend scaling simple, with conversation state owned by the client.
- Docker configuration prioritizes local reproducibility over production orchestrator complexity.

## Known Future Work

- Pluggable vector-store providers (while preserving local JSON fallback).
- Multi-knowledge-base scoping and document versioning.
- Incremental chunk updates/deletes for large document sets.
- Streaming chat responses and structured output contracts.
- Confidence/evidence coverage indicators in the UI.
