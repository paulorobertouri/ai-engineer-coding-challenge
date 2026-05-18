# ADR 001: RAG Architecture with JSON Vector Store

## Status
Accepted

## Context
The application needs to provide grounded answers based on a specific SOP document. We need a way to store document embeddings and perform efficient similarity searches locally without requiring any external database.

## Decision
We chose a Retrieval-Augmented Generation (RAG) architecture using an **in-memory cosine similarity** search backed by a **JSON file** (`vector-store.json`) as our vector store.

## Consequences
- **Local-first:** No external cloud or database dependencies for vector storage.
- **Simplicity:** Vectors are loaded into memory on startup; cosine similarity is computed in-process.
- **Portability:** The entire store is a single `.json` file, making it trivial to persist and share.
- **No runtime dependencies:** No native extension or database engine required.

---

# ADR 003: Embedding Model — `text-embedding-3-small`

## Status
Accepted

## Context
The RAG pipeline requires converting document chunks and user queries into dense vector representations (embeddings) so that semantically similar content can be retrieved by cosine similarity. The choice of model directly affects retrieval quality, cost, and response latency.

## Decision
We use OpenAI's **`text-embedding-3-small`** model for all embedding operations (both ingestion and query-time retrieval).

Key reasons for this choice over alternatives:
- **Accuracy vs. cost balance:** `text-embedding-3-small` outperforms the older `text-embedding-ada-002` on standard benchmarks (MTEB) while being cheaper per token, making it the best value option in OpenAI's embedding catalogue for a PoC workload.
- **Sufficient dimensionality:** It produces 1 536-dimensional vectors by default, which gives enough resolution for cosine similarity over the small number of SOP chunks (< 50) without adding unnecessary storage or compute overhead.
- **Native SDK support:** The OpenAI .NET SDK exposes `text-embedding-3-small` directly through `EmbeddingClient`, requiring no custom HTTP code or extra dependencies.
- **Deterministic fallback compatibility:** When no API key is present, the `DeterministicEmbeddingService` produces fixed-size vectors of the same dimensionality, preserving interface compatibility and allowing offline development and testing without changing any downstream code.

## Consequences
- **Retrieval quality:** Semantic similarity over SOP sections is accurate enough for the grocery-store domain without fine-tuning.
- **Cost:** Token costs are low — ingestion of the full SOP runs once and query embeddings are short single-sentence strings.
- **Latency:** Each embedding call adds ~100–200 ms; the Polly resilience pipeline (retry + timeout) guards against transient API failures.
- **Upgrade path:** Switching to `text-embedding-3-large` (3 072 dimensions) for higher accuracy is a one-line config change; the vector store would need to be re-ingested to match the new dimensionality.

---

# ADR 002: Modern Component-Based Frontend

## Status
Accepted

## Context
The UI needs to be accessible, responsive, and provide a high-quality user experience similar to modern AI tools like ChatGPT.

## Decision
We used **React 19** with **Tailwind CSS**, **Framer Motion** for animations, and **Lucide React** for iconography. We also integrated **Zod** for schema-driven validation.

## Consequences
- **UX:** Smooth transitions and professional layout.
- **Accessibility:** Semantic HTML and Aria attributes are used throughout.
- **Reliability:** Client-side validation prevents malformed requests.
- **Maintainability:** Tailwind's utility-first approach and modern component patterns make the codebase easy to extend.

---

# ADR 004: Security-First Backend Configuration

## Status
Accepted

## Context
The application handles business-sensitive data (SOPs) and interacts with external AI APIs. We need to ensure the system is protected against common web vulnerabilities.

## Decision
We implemented a multi-layered security approach:
- **Security Headers:** Added HSTS, X-Frame-Options (DENY), X-Content-Type-Options (nosniff), and Referrer-Policy.
- **Dependency Auditing:** Integrated \`dotnet list package --vulnerable\` and \`npm audit\` into the development and CI workflows.
- **Data Protection:** Strict request validation using Zod and Data Annotations.

## Consequences
- **Mitigation:** Reduced risk of Clickjacking, MIME-sniffing, and XSS attacks.
- **Compliance:** Aligns with modern web security standards.

---

# ADR 005: High-Performance Asynchronous I/O

## Status
Accepted

## Context
RAG operations involving embedding generation and vector search can be I/O intensive. We need to maximize throughput and responsiveness.

## Decision
We standardized on fully **Asynchronous patterns** (\`async/await\`) across all service layers:
- **Vector Store:** Async file I/O for reading and writing `vector-store.json`.
- **Network:** Leveraged async OpenAI SDK methods.
- **Compression:** Enabled Gzip/Brotli response compression in Kestrel.

## Consequences
- **Efficiency:** Better thread utilization in .NET, allowing the server to handle more concurrent users.
- **Speed:** Reduced payload sizes and faster retrieval times.

---

# ADR 006: Endpoint Style Decision - Keep Controllers (For Now)

## Status
Accepted

## Context
The backend currently exposes versioned `health`, `chat`, `ingest`, and `sources` endpoints through MVC controllers. We evaluated whether to migrate selected endpoints to Minimal API route groups.

The project has important constraints:
- Preserve API versioning and current OpenAPI output.
- Keep endpoint tests straightforward and maintainable.
- Avoid adding external endpoint framework packages.
- Keep local and Docker workflows unchanged.

## Decision
We will keep controllers as the primary endpoint style for now.

We will not introduce a mixed controller + Minimal API split for existing `chat` and `ingest` flows at this stage.

## Rationale
- Existing controllers already encode validation, authorization, and response behavior clearly using built-in ASP.NET Core patterns.
- Current API versioning setup and Swagger generation are stable and well-covered by tests and generated frontend API contracts.
- `ingest` and `chat` have non-trivial request handling where controller conventions remain readable for this codebase.
- Migrating only a subset now would add style inconsistency without a clear net readability gain.

## Consequences
- Endpoint style remains consistent across the backend.
- No additional dependencies are required.
- Existing tests and OpenAPI contract checks stay valid without migration churn.

## Revisit Trigger
Re-evaluate Minimal API route groups when:
- A new bounded endpoint area is introduced that is naturally small and function-oriented, or
- Controller orchestration becomes harder to follow than equivalent typed Minimal API handlers.

---

# ADR 007: Real-Time Transport Strategy - Keep SSE + Polling (For Now)

## Status
Accepted

## Context
The product has two real-time-like needs:
- Chat token streaming from backend to frontend.
- Ingest job progress/status updates.

We evaluated whether to add SignalR now versus keeping existing built-in HTTP approaches.

Current implementation already provides:
- Server-Sent Events (SSE) for chat streaming via `/api/v1/chat/stream`.
- HTTP polling for ingest job status via `/api/v1/ingest/jobs/{jobId}`.

The project constraints require local-first behavior without cloud dependencies and preserving deterministic local/e2e flows.

## Decision
We will not introduce SignalR at this stage.

We will continue using:
- SSE for one-way server-to-client chat stream updates.
- Polling for ingest job status transitions.

## Rationale
- Current requirements are satisfied without bidirectional connection complexity.
- Existing e2e and local workflows are already stable with SSE + polling.
- Avoiding an additional transport abstraction keeps operational behavior simpler for local and Docker runs.
- No managed service (for example Azure SignalR) is required to deliver current functionality.

## Consequences
- Real-time behavior remains cloud-independent and easy to run locally.
- Simpler HTTP fallback paths remain first-class rather than secondary.
- We defer connection lifecycle concerns (hub scaling/state/reconnect choreography) until there is a clear product need.

## Revisit Trigger
Re-evaluate SignalR when at least one of these becomes true:
- Bidirectional real-time workflows are needed (for example collaborative or operator push actions).
- Polling overhead becomes materially expensive for active job tracking.
- Multiple simultaneous live streams/events must be multiplexed per user session.

---

# ADR 008: Engineering Standards For Layering, Logging, And Local Observability

## Status
Accepted

## Context
The codebase now spans many vertical slices (ingest, chat, streaming, feedback, source viewer, auth, observability). Without explicit standards, feature growth can blur boundaries between HTTP orchestration, application workflows, provider adapters, and UI state handling.

The project also requires local-first diagnostics without dependency on paid observability platforms.

## Decision
We formalize implementation standards in `docs/engineering-standards.md` with these defaults:

- Backend layering: thin controllers, workflow handlers in `Application/*`, provider adapters behind interfaces in `Services/*`, explicit mappings, startup options validation.
- Frontend layering: typed API service modules, reusable presentation components, workflow state in page/hooks, accessibility and reduced-motion behavior as default requirements.
- Logging levels: clear guidance for debug/information/warning/error/critical usage and correlation fields.
- Local observability profile: console logs + console OpenTelemetry by default, OTLP optional, readiness/liveness checks documented for local and Docker troubleshooting.

## Consequences
- New features have predictable placement and lower coupling risk.
- Logging noise is reduced while preserving diagnostics value.
- Local debugging remains effective without cloud observability accounts.

---

# ADR 009: Health Strategy With Lightweight Liveness And Conditional Readiness Probes

## Status
Accepted

## Context
Operational checks must be meaningful for local runs, Docker health checks, and deployment automation while preserving local-first behavior without mandatory external dependencies.

## Decision
We split responsibilities clearly:

- Liveness (`GET /api/v1/health`) remains lightweight and process-focused.
- Readiness (`GET /api/v1/ready`) validates local dependencies (source document + vector-store path) and selected-mode configuration.
- Optional provider connectivity probing is available via `HealthChecks` options and only runs when OpenAI mode is active and probing is explicitly enabled.

## Rationale
- Avoids false negatives in offline/fallback mode.
- Keeps docker health checks aligned with real request-serving readiness.
- Enables deterministic tests by allowing host/port probe configuration.

## Consequences
- Readiness output is more operationally useful without leaking secrets.
- Health checks are testable in CI/local using loopback probe simulation.

---

# ADR 010: Role-Aware Response Emphasis

## Status
Accepted

## Context
Different store roles (for example cashier, manager, department lead) need the same SOP facts with different emphasis. A single generic response style can hide role-relevant action points.

## Decision
Add optional `ChatRequest.userRole` with allowed values:

- `cashier`
- `manager`
- `department_lead`

The role is used as a hint for response emphasis only and does not bypass SOP grounding or authorization.

## Consequences
- API clients can request role-specific answer framing while preserving consistent source citations.
- Validation ensures unsupported role values fail fast.
- Fallback and OpenAI modes share the same role-aware behavior contract.

---

# ADR 011: Operator Reliability Controls And Governance Workflows

## Status
Accepted

## Context
The platform now supports background ingest jobs, operational observability, and SOP mutation workflows. As production usage grows, operators need explicit controls and governance checkpoints rather than relying only on logs and ad-hoc procedures.

## Decision
We introduced the following architecture changes:

- Ingest queue controls: list, dead-letter visibility, cancel, retry, and queued-priority updates.
- In-memory per-endpoint SLO tracker with a health-facing summary endpoint for local latency and error-rate insight.
- Chaos profile middleware (opt-in) to inject synthetic failures on selected API paths for resilience tests.
- SOP mutation approval workflow that blocks production ingest activation unless the source checksum is explicitly approved.
- Retrieval benchmark dashboard endpoints with persisted run history to trend precision/recall over fixture queries.

## Consequences
- Operators can perform incident-response actions (retry/cancel/inspect failures) without direct datastore edits.
- Reliability checks are visible through API contracts and testable in CI/local environments.
- Governance controls reduce accidental unreviewed SOP mutations in production.
- Retrieval quality regressions become trendable over time rather than anecdotal.

## Revisit Trigger
Re-evaluate when multi-tenant approval delegation, persistent job queues, or external SLO backends are introduced.

---

# ADR 001: Use Polly for Resilience
- **Context**: The backend requires resilience for external API calls.
- **Decision**: Use Polly for retries, exponential backoff, and circuit breaking.
- **Consequences**: Improved reliability and fault tolerance.

---

# ADR 002: Playwright for E2E Testing
- **Context**: The project needs robust E2E testing for user flows.
- **Decision**: Use Playwright for its rich API and cross-browser support.
- **Consequences**: Enhanced test coverage and reliability.

