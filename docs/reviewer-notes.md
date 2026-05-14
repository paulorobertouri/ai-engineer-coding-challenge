# Reviewer Notes: Tradeoffs And Future Work

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
