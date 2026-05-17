# Challenge Review Notes And Roadmap

This document is a reviewer-facing summary of architectural intent, explicit tradeoffs, and next-step roadmap.

Detailed forward-looking backlog items remain in `TODO.MD`.

## Latest implemented changes (2026-05-17)

- Health strategy now separates lightweight liveness from dependency-aware readiness.
- Optional OpenAI readiness connectivity probe can be enabled without impacting fallback mode.
- Engineering standards now define backend/frontend layering and logging/observability expectations.

## Intent and constraints

- Local-first and free-to-run experience is the default path.
- External cloud dependencies are optional adapters, not hard requirements.
- Offline/fallback behavior is supported when `OpenAI:ApiKey` is not configured.
- CI favors reproducible checks that can run in public runners without paid services.

## Key architectural decisions

- Backend: ASP.NET Core (.NET 10), controller-based API, strongly typed options binding.
- Frontend: React + TypeScript + Vite with generated OpenAPI contracts for client/server drift control.
- Data storage: JSON file-backed vector store for local simplicity and deterministic challenge runs.
- Retrieval/chat mode selection:
  - OpenAI mode when API key is configured.
  - Deterministic fallback mode otherwise.

## Tradeoffs

### Local-first + fallback mode

Benefits:

- Fully runnable for reviewers without cloud accounts.
- Deterministic behavior for baseline testing and evidence capture.

Costs:

- Lower answer quality than hosted LLM mode.
- Limited semantic recall versus production-grade embeddings + ANN backends.

### JSON vector store

Benefits:

- Simple to inspect, debug, and reset locally.
- No infrastructure setup required.

Costs:

- Not optimized for high scale or large corpora.
- Concurrency/performance characteristics are intentionally basic.

### RAG limitations (intentional for challenge scope)

- Retrieval quality depends on chunking and lexical reranking choices.
- Citation confidence is heuristic, not a formal grounding guarantee.
- Fallback mode can miss nuanced policy interpretation.
- Ingestion pipeline is designed for local safety and clarity over throughput.

## Testing strategy

- Backend unit/integration tests for controllers, services, contract checks, resilience scenarios.
- Frontend unit tests for API client, components, and page behavior.
- E2E Playwright flow tests for ingest/chat/citation scenarios.
- Visual regression baselines for setup/chat/citation-source flows.
- CI contract drift checks (`check:api-types`) to keep frontend/backend API compatibility explicit.
- Supply-chain checks include secret scan, image vulnerability scan, SBOM generation, and license policy checks.

## Roadmap (near term)

- Strengthen dependency license governance and contributor policy docs.
- Review .NET 10 idioms and startup composition for simplification opportunities.
- Evaluate selected Minimal API route groups where it improves clarity.
- Expand upload validation with broader signature coverage and optional malware scanning hooks.
- Extend retention tooling with optional scheduled cleanup.

## Roadmap (future)

- Optional provider adapters (hosted vector DB and alternative LLM providers).
- Stronger observability defaults for production-like deployments.
- Performance benchmark suite for larger datasets and concurrent chat load.
- UX improvements around source explainability and operator workflows.
