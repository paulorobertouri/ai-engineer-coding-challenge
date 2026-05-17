# Engineering Standards

This document defines implementation defaults for backend and frontend changes so the codebase remains consistent as features evolve.

## Backend Design Patterns

### Layer boundaries

- `Controllers/*` exposes HTTP concerns only: route attributes, auth/policy gates, model validation outcomes, and HTTP result shaping.
- `Application/*` owns feature workflows via focused handlers (`Query`/`Command` style).
- `Contracts/*` defines API request/response DTOs and transport-only models.
- `Models/*` contains internal domain/value models shared across services.
- `Services/*` contains infrastructure adapters and core behavior behind interfaces (OpenAI, vector store, extraction, guardrails, storage).
- `Options/*` centralizes strongly typed configuration with startup validation.

### Dependency boundaries

- Provider-specific code must stay behind interfaces in `Services/*`.
- Controllers and application handlers do not instantiate provider SDK clients directly.
- Configuration-driven provider selection happens in startup registration (`Program.cs`) and is validated at startup.

### Coding defaults

- Keep endpoints thin and move orchestration into handlers/services.
- Prefer constructor injection and immutable options snapshots (`IOptions<T>`).
- Keep mapping explicit in code (no convention/reflection mappers).
- Pass cancellation tokens from controller to downstream service boundaries.
- Return `ProblemDetails` for non-success HTTP responses.

### File placement checklist

- New endpoint DTO: `Contracts/*`
- New endpoint orchestration: `Application/<Feature>/*`
- New provider adapter: `Services/*` + interface
- New config key: `Options/*` + `Program.cs` `AddValidatedOptions<T>()`

## Frontend Design Patterns

### Feature structure

- `src/services/*` contains API communication and transport parsing.
- `src/types/*` contains UI/domain shapes and schema validation.
- `src/components/*` contains reusable UI pieces with minimal business logic.
- `src/pages/*` (or top-level page components) coordinate workflow state.
- Repeated stateful workflow logic should move to custom hooks.

### State and API rules

- Keep API calls inside typed client modules, not directly inside leaf UI components.
- Keep request/response parsing resilient to `ProblemDetails` and typed error codes.
- Prefer React built-ins (`useState`, `useMemo`, `useEffect`, `useCallback`) before adding extra state libraries.
- Preserve keyboard-first interactions, status messaging, and reduced-motion behavior for all user-facing flows.

### Component behavior defaults

- Components should receive data and callbacks through props; avoid hidden global coupling.
- Keep mobile layout behavior explicit for chat/citation/sidebar experiences.
- Guard external links and markdown rendering via explicit protocol allowlists.

## Logging Level Guidelines

Use these rules across backend logs so signal remains high.

- `Debug`: noisy developer diagnostics, temporary deep troubleshooting, payload-size counters.
- `Information`: successful high-value milestones (ingest started/completed, chat completed, mode selection, readiness checks).
- `Warning`: degraded but handled behavior (fallback mode active, low-confidence/no-context answer, retry attempts, readiness probe skipped/failed, circuit half-open/open transitions).
- `Error`: failed operations that require intervention (ingest failure, unhandled exception, provider call failure after retries).
- `Critical`: process-level or data-integrity risk requiring urgent action (startup cannot continue, unrecoverable storage corruption).

Rules:

- Include correlation fields (`RequestId`, `ConversationId`, `KnowledgeBaseId`, `ErrorCode`) whenever available.
- Do not log raw prompts, full SOP content, secrets, or API keys.
- Expected user errors (validation/conflict/rate-limit) should not emit `Error` unless unexpected side effects occur.

## Local Observability Profile

Local debugging must remain usable without external SaaS.

### Default profile

- Console structured logs enabled in local process and Docker runs.
- OpenTelemetry console exporter enabled by default.
- Optional OTLP exporter enabled only when `Observability:OtlpEndpoint` is configured.
- Container file logs are written under `/app/Data/Logs` (persisted by `backend_data` volume).

### Local troubleshooting flow

1. Check backend logs (`docker compose logs backend` or local console output).
2. Call liveness/readiness endpoints:
   - `GET /api/v1/health` for process liveness and mode summary
   - `GET /api/v1/ready` for dependency/provider readiness checks
3. If needed, enable `HealthChecks:EnableOpenAIConnectivityProbe=true` to run OpenAI network probe in OpenAI mode.
4. Use `Observability:OtlpEndpoint` only when running a local collector (optional).

### Minimal required config keys

- `Observability:Enabled`
- `Observability:EnableConsoleExporter`
- `Observability:OtlpEndpoint` (optional)
- `HealthChecks:EnableOpenAIConnectivityProbe`
- `HealthChecks:OpenAIProbeHost`
- `HealthChecks:OpenAIProbeTimeoutMilliseconds`
