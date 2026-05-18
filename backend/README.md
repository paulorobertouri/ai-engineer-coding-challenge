# Backend

.NET 10 Web API for the Grocery Store SOP Assistant.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/health` | Lightweight liveness endpoint for process status and active AI mode summary |
| `GET` | `/api/v1/ready` | Readiness status (`ready`/`not_ready`) based on source document, vector-store path access, selected mode configuration, and optional OpenAI connectivity probe |
| `POST` | `/api/v1/ingest` | Chunk → embed → persist the SOP document (`forceReingest=true` enables reingest with embedding reuse by `ContentHash`) |
| `POST` | `/api/v1/ingest?knowledgeBaseId=<id>` | Ingest into a specific knowledge-base scope (defaults to `default`) |
| `POST` | `/api/v1/ingest/upload?knowledgeBaseId=<id>` | Upload and ingest `.md`, `.txt`, `.pdf`, and `.docx` files locally (scanned-image OCR is optional) |
| `POST` | `/api/v1/ingest/preview` | Chunk-only preview (no embeddings or vector-store writes) for configured source or uploaded file |
| `DELETE` | `/api/v1/ingest/reset?confirm=RESET` | Development-only explicit reset of the vector store to allow reingestion |
| `POST` | `/api/v1/chat` | RAG-grounded multi-turn chat with tool-calling support |
| `POST` | `/api/v1/chat/stream` | SSE streaming variant that emits progressive `delta` events and a final `complete` payload |

## Services

| Interface | Production Implementation | Fallback (no API key) | Description |
|---|---|---|---|
| `IChunkingService` | `MarkdownChunkingService` | _(same)_ | Splits markdown on `#` level-1 and `##` level-2 headers into semantic chunks using a source-generated regex |
| `IDocumentExtractionService` | `LocalDocumentExtractionService` | _(same)_ | Extracts plain text from `.md`, `.txt`, `.pdf`, and `.docx` locally; OCR adapters for scanned images are optional |
| `IEmbeddingService` | `OpenAIEmbeddingService` | `DeterministicEmbeddingService` | Generates embeddings via `text-embedding-3-small`; fallback uses FNV1a hashing |
| `IVectorStoreService` | `JsonVectorStoreService` | _(same)_ | Provider contract for load/save/search/delete/filter with startup-validated selection (`VectorStore:Provider`, default `json`) |
| `IRetrievalChatService` | `OpenAIRetrievalChatService` | `FallbackRetrievalChatService` | RAG pipeline with Polly resilience; fallback uses keyword matching |

Service registration in `Program.cs` is conditional: when `OpenAI:ApiKey` is present the real OpenAI services (including `OpenAIClient`) are wired; otherwise the deterministic fallbacks are used. The health endpoint dynamically reflects the active mode in its `notes` response field.

Startup validation:
- All options classes are bound through `AddValidatedOptions<T>()` with `ValidateOnStart()`
- This fails startup fast for invalid configuration before requests are accepted
- Readiness adds runtime dependency checks, while liveness stays dependency-light

## Tool Calling

`OpenAIRetrievalChatService` defines two OpenAI function tools:

| Tool | Parameters | Behaviour |
|---|---|---|
| `search_sop` | `query: string` | Re-embeds the query and retrieves up to `Retrieval:TopK` chunks above `Retrieval:MinSimilarityScore` |

Tool enablement is server-controlled by `OpenAI:EnableTools` (the client request flag is ignored for execution policy).

When `finish_reason` is `tool_calls` each tool is executed, its result appended as a `tool` message, and the model called a second time to produce the final response.

Tool-call observability:
- Invalid JSON tool arguments are logged with `ConversationId`, `ToolCallId`, and `ToolName`
- Empty `query` arguments are logged with the same structured fields
- Tool calls that produce no relevant matches above threshold are logged with `ConversationId`, `ToolCallId`, `ToolName`, and `Threshold`

Authorization:
- Local API-key auth uses the `X-Api-Key` header by default and is disabled for anonymous access in non-development environments unless a key is configured
- `ChatController` requires the `ChatUser` policy
- `IngestController` preview operations require the `Operator` policy
- `IngestController` ingest, upload, and reset operations require the `KnowledgeAdmin` policy
- Development mode keeps protected endpoints accessible without an identity provider so local demos remain frictionless

## Vector Store

- File: `Data/vector-store.json` (configurable via `Challenge__VectorStorePath`)
- Provider: `VectorStore__Provider=json` by default (startup validation currently allows `json` only; future providers are opt-in)
- Inside Docker the file lives at `/app/Data/vector-store.json` (persisted in the `backend_data` named volume)
- Format: JSON array of `VectorRecord` objects (`id`, `source`, `chunkText`, `embedding`, `metadata`)
- Chunk IDs are deterministic (`<source>-<section>-<index>-<hash>`) to keep citations/logs stable across unchanged reingests
- Metadata includes stable `ContentHash` per chunk for change detection
- Metadata includes `KnowledgeBaseId` (default scope: `default`) so multiple SOP sets can coexist in one store
- Metadata includes `SourceChecksum`, `DocumentVersion`, and `IngestedAtUtc` so citations can identify the document version used for an answer
- During forced reingest, unchanged `ContentHash` values reuse existing embeddings to avoid recomputation
- Search: cosine similarity computed in-process over all records (suitable for POC scale)

Knowledge-base scoping:
- Ingest and chat requests accept optional `knowledgeBaseId`
- If omitted, the backend uses `default`
- Legacy records without explicit `KnowledgeBaseId` metadata are treated as `default`
- Reingesting one knowledge base replaces only that knowledge base records, preserving other scopes

Document versioning:
- Each ingest computes a SHA-256 checksum for the source document text
- If no explicit version is supplied, the backend derives a local version label such as `sha256:abcd1234ef56`
- Citations expose the `documentVersion` and checksum metadata for traceability

Duplicate detection:
- Before chunking and embedding, ingest computes the source checksum and checks existing records in the same knowledge base
- If the checksum already exists, ingest returns `409 conflict` with the existing version and checksum in the response detail

Incremental updates and deletes:
- Forced reingest performs a per-chunk diff inside the selected knowledge base
- Unchanged chunks are preserved without recomputing embeddings
- Changed or new chunks are rebuilt, while removed source sections are omitted from the saved store
- Other knowledge-base scopes remain untouched during the update

To reset persisted Docker ingestion data intentionally:

```bash
docker compose down -v
```

For local development without Docker volume reset, the API also supports a guarded reset endpoint:

- Only available when `ASPNETCORE_ENVIRONMENT=Development`
- Requires explicit query confirmation: `DELETE /api/v1/ingest/reset?confirm=RESET`
- Clears the vector store atomically so reingestion can happen safely

## Resilience

`OpenAIRetrievalChatService` wraps every OpenAI call in a Polly pipeline:
- Exponential backoff retry — 3 attempts, 2 s base delay
- 30 s timeout
- Circuit breaker to fail fast when provider instability exceeds configured thresholds

## OpenTelemetry

OpenTelemetry instrumentation is enabled for ASP.NET Core requests and custom application spans/metrics:
- Traces: request pipeline, ingestion processing, vector search, and chat generation paths
- Metrics: chat request counts/latency, vector search latency, and ingest chunk/latency metrics
- Exporters: console exporter enabled by default; OTLP exporter is optional via configuration
- Sensitive-content safety: telemetry tags record IDs, counts, and timings only (not raw document/chat content)

## Observability

Structured logs for retrieval/chat flows now include:
- `ConversationId`, `Mode` (`openai` or `fallback`), and selected model
- Retrieved chunk IDs and similarity scores
- Completion latency and total request latency
- Warnings when vector store candidate retrieval is empty or tool calls have invalid/empty arguments
- Guardrail escalation category metadata when rule-based guardrails intercept a query

Retrieval is threshold-aware in both OpenAI and fallback chat services:
- Candidate chunks are limited by `Retrieval:TopK`
- Optional reranking can score a larger candidate pool before final `TopK` selection
- Chunks below `Retrieval:MinSimilarityScore` are filtered out
- If no chunk passes the threshold, the assistant returns a grounded "not enough information in the SOP" response with no citations
- Follow-up retrieval query rewriting is enabled by `Retrieval:EnableQueryRewriting` and logs only rewrite metadata/lengths by default

Streaming behavior:
- `POST /api/v1/chat` remains the stable non-streaming JSON endpoint
- `POST /api/v1/chat/stream` uses `text/event-stream`
- Server emits `event: delta` chunks followed by one `event: complete` JSON payload
- Fallback/no-key mode also supports streaming because the stream endpoint wraps the same retrieval service contract

Guardrails behavior:
- Rule-based categories can intercept high-risk topics (for example medical/legal/emergency/HR)
- Intercepted requests return escalation language with no citations
- Structured output `refusalReason` is set to category-specific values like `guardrail_medical`

Citations returned by `/api/v1/chat` now include richer metadata for traceability:

Structured chat output:
- `ChatResponse.structuredOutput.answerText` mirrors the returned answer text in validated form
- `ChatResponse.structuredOutput.citedChunkIds` explicitly lists cited chunk IDs
- `ChatResponse.structuredOutput.refusalReason` is set to `not_found` when no relevant SOP context exists
- `ChatResponse.structuredOutput.followUpSuggestions` is reserved for optional follow-up prompts
- `ChatResponse.confidence.level` surfaces `high`, `medium`, `low`, or `not_found`
- `ChatResponse.confidence.evidenceCoverage` reports the cited-chunk coverage ratio from `0` to `1`

Usage telemetry:
- `ChatResponse.usage.source` is `sdk`, `estimated`, `fallback`, or `guardrail`
- `ChatResponse.usage.promptTokens`, `completionTokens`, and `embeddingTokens` expose the token counts used for estimation
- `ChatResponse.usage.estimatedCostUsd` uses configured per-model pricing when available and stays `0` in fallback/guardrail mode
## Rate Limiting

Fixed-window rate limiting is applied per client IP via ASP.NET Core's built-in rate limiter:

| Endpoint | Policy | Limit |
|---|---|---|
| `POST /api/v1/chat` | `chat` | 30 requests / minute |
| `POST /api/v1/ingest` | `ingest` | 10 requests / minute |

Exceeding the limit returns `429 Too Many Requests`. Client IP is resolved through forwarded headers (`X-Forwarded-For`) to work correctly behind reverse proxies and Docker.

## Error Responses

API error payloads are standardized on `ProblemDetails` / `ValidationProblemDetails` with typed `extensions.code` values:

- `validation_error` (400)
- `bad_request` (400)
- `conflict` (409)
- `not_found` (404)
- `rate_limit_exceeded` (429)
- `internal_server_error` (500)

This applies to controller validation/ingest errors, rate-limit rejections, and global exception handling.

## Configuration

| Key | Default | Description |
|---|---|---|
| `OpenAI:ApiKey` | _(empty)_ | OpenAI API key — triggers fallback mode if absent |
| `OpenAI:EnableTools` | `true` | Enables/disables OpenAI tool calling on the server |
| `OpenAI:ChatModel` | `gpt-5.4-mini` | Chat completion model |
| `OpenAI:EmbeddingModel` | `text-embedding-3-small` | Embedding model |
| `OpenAI:CircuitBreaker:Enabled` | `true` | Enables OpenAI circuit-breaker behavior |
| `OpenAI:CircuitBreaker:FailureRatio` | `0.5` | Fraction of handled failures needed to open the circuit |
| `OpenAI:CircuitBreaker:MinimumThroughput` | `8` | Minimum sampled requests before evaluating failures |
| `OpenAI:CircuitBreaker:SamplingDurationSeconds` | `30` | Sliding evaluation window for failure ratio |
| `OpenAI:CircuitBreaker:BreakDurationSeconds` | `30` | Time the circuit remains open before half-open probe |
| `Retrieval:TopK` | `3` | Maximum number of retrieved chunks considered for response context |
| `Retrieval:MinSimilarityScore` | `0.3` | Minimum cosine similarity score for a chunk to be considered relevant |
| `Retrieval:EnableQueryRewriting` | `true` | Rewrites follow-up user messages into standalone retrieval queries |
| `Retrieval:EnableReranking` | `true` | Enables local lexical reranking after vector retrieval |
| `Retrieval:RerankCandidateMultiplier` | `3` | Multiplies `TopK` to build the reranking candidate pool |
| `Guardrails:EnableRuleBasedGuardrails` | `true` | Enables local rule-based safety/escalation interception |
| `Guardrails:Categories` | _(see appsettings)_ | Configurable category keywords and escalation messages |
| `Challenge:SourceDocumentPath` | `../../../../knowledge-base/Grocery_Store_SOP.md` | Path to the SOP markdown file |
| `Challenge:VectorStorePath` | `Data/vector-store.json` | Path for vector store persistence |
| `VectorStore:Provider` | `json` | Vector store provider key, validated at startup |
| `IngestRequest.knowledgeBaseId` | `default` | Optional scope for ingestion; isolates records by SOP set |
| `ChatRequest.knowledgeBaseId` | `default` | Optional scope for retrieval and citations |
| `ChatRequest.userRole` | _(empty)_ | Optional role hint (`cashier`, `manager`, `department_lead`) to tailor response emphasis |
| `RateLimiting:Chat:PermitLimit` | `30` | Chat requests allowed per window |
| `RateLimiting:Chat:WindowSeconds` | `60` | Chat rate-limit window size in seconds |
| `RateLimiting:Chat:QueueLimit` | `0` | Queue size for excess chat requests |
| `RateLimiting:Ingest:PermitLimit` | `10` | Ingest requests allowed per window |
| `RateLimiting:Ingest:WindowSeconds` | `60` | Ingest rate-limit window size in seconds |
| `RateLimiting:Ingest:QueueLimit` | `0` | Queue size for excess ingest requests |
| `RateLimiting:Mode` | `inmemory` | `inmemory` keeps ASP.NET fixed-window limits; `distributed` enables distributed-store middleware |
| `RateLimiting:Distributed:Provider` | `memory` | Distributed provider key (`memory` built-in, `redis` reserved for optional adapter) |
| `Upload:MaxUploadBytes` | `10485760` | Maximum upload size in bytes for `/ingest/upload` |
| Upload formats | `.md`, `.txt`, `.pdf`, `.docx` | Default local extraction formats for `/ingest/upload` |
| Optional OCR formats | `.png`, `.jpg`, `.jpeg`, `.tif`, `.tiff`, `.bmp` | Require a registered local OCR adapter to enable scanned document extraction |
| `Auth:ApiKey` | _(empty)_ | Local API key used by the built-in authentication handler when admin endpoints are protected |
| `Auth:ApiKeyHeaderName` | `X-Api-Key` | Header name read by the local API-key auth handler |
| `Auth:AllowAnonymousInDevelopment` | `true` | Lets local development access protected admin endpoints without an identity provider |
| `Observability:Enabled` | `true` | Enables OpenTelemetry tracing and metrics wiring |
| `Observability:EnableConsoleExporter` | `true` | Emits traces/metrics to console without any cloud dependency |
| `Observability:OtlpEndpoint` | _(empty)_ | Optional OTLP endpoint URL (for example `http://localhost:4317`) |
| `HealthChecks:EnableOpenAIConnectivityProbe` | `false` | Enables optional OpenAI network connectivity probe for readiness when OpenAI mode is active |
| `HealthChecks:OpenAIProbeHost` | `api.openai.com` | Host used for the optional OpenAI readiness connectivity probe |
| `HealthChecks:OpenAIProbePort` | `443` | Port used for the optional OpenAI readiness connectivity probe |
| `HealthChecks:OpenAIProbeTimeoutMilliseconds` | `1200` | Timeout for optional OpenAI connectivity probe |
| `Cors:AllowedOrigins` | `["http://localhost:5173"]` | Allowed CORS origins |

For backend/frontend implementation boundaries and logging guidelines, see `docs/engineering-standards.md`.

## Local Development

```bash
cd backend/src/Api
dotnet run
```

Requires the root `.env` (or equivalent environment variables) to be in scope. See the root `README.md` for configuration details.

## Testing

```bash
# from repo root
./scripts/test.sh backend

# or directly
dotnet test backend/src/Api.Tests/Api.Tests.csproj
```

PowerShell on Windows:

```powershell
.\scripts\test.ps1 backend
```

Test coverage:
- `MarkdownChunkingServiceTests` — verifies chunking by `#`/`##` headers and empty-input handling
- `JsonVectorStoreServiceTests` — verifies cosine similarity ordering, missing-file graceful load, and save/load round-trip
- `JsonVectorStoreServiceAdditionalTests` — verifies thread-safe double-checked locking, metadata filtering, delete-by-id, and edge cases
- `VectorStoreOptionsValidatorTests` — verifies provider selection startup validation rules
- `DeterministicEmbeddingServiceTests` — verifies FNV1a determinism and dimensionality
- `FallbackRetrievalChatServiceTests` — verifies keyword-based response generation plus prompt-injection and out-of-scope grounding behavior
- `RagEvaluationFixtureTests` — runs fixture-driven retrieval/grounding checks without paid OpenAI calls
- `RuleBasedUserQueryGuardrailServiceTests` — verifies category matching and escalation behavior
- `GetReadinessQueryHandlerTests` — verifies readiness behavior for missing dependencies and OpenAI probe disabled/failure/success paths
- `ChatControllerTests` — verifies chat endpoint request handling and validation
- `HealthControllerTests` — verifies mode-aware notes in both API-key and fallback modes
- `IngestControllerTests` — verifies path resolution, ingestion pipeline, and 404 on missing document

## Formatting

```bash
# check only
./scripts/format.sh

# auto-fix
./scripts/format.sh --fix
```

PowerShell on Windows:

```powershell
.\scripts\format.ps1
.\scripts\format.ps1 --fix
```
