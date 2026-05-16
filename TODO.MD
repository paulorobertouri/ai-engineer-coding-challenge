# TODO

## Definition Of Done For Every Task

Before marking any task complete, verify the project is still formatted, linted, tested, building, and stable.

- [ ] Run the relevant formatter/linter checks:
  - Backend: `./scripts/format.sh` or `dotnet format backend/AIEngineerCodingChallenge.Backend.slnx --verify-no-changes`.
  - Frontend/e2e: `npm run lint`, Prettier/format checks where applicable, or `./scripts/format.sh`.
- [ ] Run the relevant tests:
  - Backend: `./scripts/test.sh backend` or `dotnet test backend/src/Api.Tests/Api.Tests.csproj`.
  - Frontend: `./scripts/test.sh frontend` or `npm test --prefix frontend`.
  - E2E: `./scripts/e2e.sh` when the task touches full-stack behavior, routing, Docker, ingest/chat flow, or UI workflows.
- [ ] Run the relevant builds:
  - Backend: `dotnet build backend/AIEngineerCodingChallenge.Backend.slnx`.
  - Frontend: `npm run build --prefix frontend`.
  - Docker: `./scripts/build.sh` or `docker compose build` when Dockerfiles, compose, runtime config, ports, or container behavior change.
- [ ] Confirm local execution still works without external services:
  - No OpenAI API key fallback mode still works.
  - Local non-Docker workflow still works for touched areas.
  - Docker Compose workflow still works for touched areas when Docker-related behavior changes.
- [ ] Update tests and documentation for changed behavior:
  - Root `README.md` when setup, scripts, architecture summary, Docker, testing, or user-facing behavior changes.
  - `backend/README.md` when backend endpoints, configuration, services, health/readiness, logging, security, or operational behavior changes.
  - `frontend/README.md` when frontend workflows, environment variables, scripts, UI behavior, or testing changes.
  - ADRs in `docs/adr/` when architecture patterns, provider choices, storage strategy, endpoint style, mediator usage, observability, or security posture change.
  - Supporting docs such as `CONTRIBUTING.md`, `docs/testing.md`, dependency policy, or review notes when the change affects contributor workflow.
  - `.env.example`, script docs, and config tables when configuration keys are added, renamed, or removed.
- [ ] Do not mark the task complete if formatting, linting, tests, build, or required local stability checks are failing.

## Highest Impact

### 1. Make Ingestion Atomic

- [x] Prevent concurrent ingest requests from both passing the empty-store check and overwriting each other.

**Why**

The ingest endpoints currently load the vector store, check whether records exist, then process and save new records. Those steps are not atomic. If two requests arrive at nearly the same time, both can observe an empty store and both can proceed.

**How**

- Move the "ingest only if empty" behavior behind a single locked service operation, or add a dedicated ingestion lock around the entire check -> chunk -> embed -> save flow.
- Keep the current conflict behavior when the store already has records.
- Add a regression test that starts two ingest operations concurrently and verifies that only one succeeds.

**Acceptance Criteria**

- Only one ingest request can succeed when multiple requests are submitted concurrently.
- Losing requests return a clear `409 Conflict`.
- Existing single-request ingest behavior remains unchanged.

### 2. Add Backend API Payload Limits

- [x] Enforce backend limits for chat payload size, not just frontend history trimming.

**Why**

The frontend caps history, but clients can call the API directly. Without backend limits, large payloads can increase token costs, memory use, latency, and abuse risk.

**How**

- Add validation attributes or custom validation for:
  - Maximum number of chat messages.
  - Maximum message content length.
  - Maximum conversation ID length.
  - At least one `user` message.
- Mirror the same values in frontend Zod schemas.
- Return consistent validation errors using `ProblemDetails`.

**Acceptance Criteria**

- Oversized chat requests fail before reaching the retrieval/chat service.
- Unit tests cover each limit.
- Valid existing frontend requests still pass.

### 3. Improve RAG Retrieval Quality

- [x] Add configurable retrieval behavior: `topK`, score thresholds, and no-relevant-context handling.

**Why**

The current retrieval flow always sends the top 3 chunks to the model, even if the best match is weak. This can cause the assistant to answer from poor context instead of saying the SOP does not contain enough information.

**How**

- Move `topK` into configuration.
- Add a minimum similarity threshold.
- If no match passes the threshold, return a grounded "not found in SOP" response or pass an empty context with stricter instructions.
- Include retrieval scores in logs and optionally in citation metadata.
- Consider hybrid lexical + vector retrieval later if semantic search misses exact SOP terms.

**Acceptance Criteria**

- Weak matches are filtered out.
- The assistant gives a clear "not in the SOP" style answer when no relevant context exists.
- Tests cover high-confidence matches, low-confidence matches, and empty vector store behavior.

### 4. Tighten Tool-Calling Behavior

- [x] Make tool use server-controlled and more observable.

**Why**

The client currently sends `useTools`, and tool calls are handled for one additional model round. That is fine for a proof of concept, but production behavior should be governed server-side and should expose failures clearly.

**How**

- Add a backend configuration option such as `OpenAI:EnableTools`.
- Ignore or remove client authority over tool enablement.
- Return/log when a tool call has invalid JSON, an empty query, or no useful matches.
- Consider a maximum tool-call loop count if multi-step tool use is ever added.

**Acceptance Criteria**

- Tool usage can be enabled or disabled by backend configuration.
- Invalid or empty tool calls are logged with useful structured fields.
- Tests cover tool-disabled, invalid-arguments, and empty-result scenarios.

### 5. Add Modern Security Headers

- [x] Add current browser security headers and remove legacy ones.

**Why**

The backend has useful baseline headers, but it does not include a Content Security Policy. It also sends `X-XSS-Protection`, which is obsolete in modern browsers.

**How**

- Add `Content-Security-Policy` appropriate for the API and Swagger.
- Add `Permissions-Policy`.
- Remove `X-XSS-Protection`.
- Add equivalent headers in the nginx frontend container if the app is served from there.
- Be careful not to break Swagger UI in development.

**Acceptance Criteria**

- API responses include the new headers.
- Swagger still works in development.
- Frontend static responses include compatible headers when served by nginx.

## Very Worthwhile

### 6. Make Docker Vector-Store Persistence Explicit

- [x] Decide whether Docker mode should persist vector-store data or be intentionally ephemeral.

**Why**

The compose file currently leaves the data volume commented out. That can surprise users because ingested records may disappear after container rebuilds.

**How**

- Either enable a named volume or bind mount for `/app/Data`, or document that Docker mode is intentionally resettable.
- If persistence is enabled, make sure permissions work for the container user.
- Update README setup and troubleshooting notes.

**Acceptance Criteria**

- Docker behavior is obvious from `docker-compose.yml` and README.
- Ingested data either survives restarts by design or is explicitly documented as temporary.

### 7. Add Frontend Production Build To CI

- [x] Run `npm run build` in the frontend CI job.

**Why**

Lint and unit tests do not catch every production build issue. Vite and TypeScript build settings can fail even when tests pass.

**How**

- Add a build step after tests in `.github/workflows/ci.yml`.
- Keep the current npm cache configuration.

**Acceptance Criteria**

- CI fails if the frontend production build fails.
- Existing frontend lint and test steps still run.

### 8. Add Playwright E2E Coverage To CI

- [x] Run Playwright tests in CI against the full stack.

**Why**

Unit tests cover the parts, but e2e tests validate that the backend, frontend, Docker setup, CORS, ingest flow, and chat flow work together.

**How**

- Add a separate CI job for e2e.
- Start the app with Docker Compose or dedicated local server commands.
- Use fallback/no-key mode so CI does not require an OpenAI API key.
- Upload Playwright traces/screenshots on failure.

**Acceptance Criteria**

- CI runs the existing Playwright chat flow.
- Failure artifacts are available for debugging.
- E2E job does not depend on paid API calls.

### 9. Split Health And Readiness Checks

- [x] Add a readiness endpoint separate from basic health.

**Why**

The current health endpoint says the service is running and reports mode notes. Operationally, it is useful to know whether dependencies and configured files are ready too.

**How**

- Keep `/health` lightweight for process liveness.
- Add `/ready` or extend health checks with readiness details.
- Check source document availability, vector-store path readability/writability, and active AI mode.
- Consider using ASP.NET Core health checks if it fits cleanly.

**Acceptance Criteria**

- Liveness and readiness are distinguishable.
- Docker or deployment tooling can use readiness before routing traffic.
- Tests cover ready and not-ready states.

### 10. Improve Observability

- [x] Add structured logging around RAG and OpenAI calls.

**Why**

When RAG behavior is wrong, logs need to answer: what was retrieved, how strong were the matches, which model ran, how long did calls take, and whether fallback mode was active.

**How**

- Log conversation ID, selected model, top chunk IDs, retrieval scores, fallback/real AI mode, and latency.
- Avoid logging raw user messages or full SOP content by default.
- Add warning logs for empty vector stores and low-confidence retrieval.

**Acceptance Criteria**

- Logs make failed or low-quality answers diagnosable.
- Sensitive content is not logged by default.
- Existing tests still pass without brittle log assertions.

## Nice Polish

### 11. Move Hardcoded Defaults Into Options Classes

- [x] Replace scattered constants and configuration lookups with strongly typed options.

**Why**

Defaults such as model names, retry counts, upload limits, paths, retrieval `topK`, and rate limits are currently spread across files. Typed options make behavior easier to discover, validate, and override.

**How**

- Add options classes for `OpenAI`, `Challenge`, `Retrieval`, `RateLimiting`, and upload limits.
- Register with `Bind`, `ValidateDataAnnotations`, and `ValidateOnStart`.
- Update services/controllers to depend on `IOptions<T>` where appropriate.

**Acceptance Criteria**

- Invalid configuration fails fast at startup.
- README configuration table matches the options classes.
- Existing defaults remain compatible.

### 12. Improve Citations

- [x] Return richer and more accurate citation metadata.

**Why**

Current citations show the source and a short chunk snippet. Users would benefit from section names, line ranges, scores, and citations that better match the final answer.

**How**

- Preserve section headings and line ranges during chunking.
- Include retrieval score and chunk ID in citation DTOs if useful for debugging.
- Filter citations to chunks above the relevance threshold.
- Consider asking the model to cite chunk IDs explicitly, then map those IDs back to citations.

**Acceptance Criteria**

- Citations help users locate the supporting SOP section quickly.
- Irrelevant low-score chunks are not shown as citations.
- Frontend rendering remains readable on desktop and mobile.

### 13. Add Controlled Reset/Reingest Flow

- [x] Add a safe way to reset and reingest the knowledge base.

**Why**

The one-time ingest rule is safe for a challenge, but real development and operations need a way to update the SOP without manually deleting files.

**How**

- Add an admin-only or development-only reset endpoint.
- Require explicit confirmation or configuration gate.
- Delete or replace the vector store atomically.
- Add frontend support only if it fits the product flow.

**Acceptance Criteria**

- Reset/reingest cannot be triggered accidentally in production.
- Development reset is easy and documented.
- Tests cover authorized and unauthorized reset behavior.

### 14. Support Runtime Frontend API Configuration

- [x] Avoid baking the backend API URL into the frontend Docker image.

**Why**

`VITE_API_BASE_URL` is build-time configuration. That means the same frontend image cannot easily move between environments with different backend URLs.

**How**

- Serve a small `/config.json` from nginx or inject runtime environment variables through an entrypoint script.
- Load the API base URL before creating the API client.
- Keep the current Vite env fallback for local development.

**Acceptance Criteria**

- The same frontend Docker image can run against different backend URLs.
- Local development remains simple.
- Missing runtime config falls back to a sensible default or clear error.

## Second Wave Improvements

### 15. Improve Chat UX

- [x] Add small workflow improvements that make the assistant easier to use repeatedly.

**Why**

The current UI covers the core ingest/chat loop well, but users will quickly expect common chat affordances such as copying answers, retrying failures, starting a new conversation, and understanding when the app is in offline mode.

**How**

- Add a copy-answer button to assistant responses.
- Add a retry action for failed chat and ingest requests.
- Add a new-chat/reset-conversation button.
- Show a visible offline/fallback-mode badge when the backend reports no OpenAI key.
- Preserve chat history in `localStorage` or session storage so refreshes do not wipe the current session.

**Acceptance Criteria**

- Users can copy assistant answers without selecting text manually.
- Failed requests can be retried without retyping the prompt.
- Users can intentionally start a fresh conversation.
- Offline/fallback mode is visible in the UI.

### 16. Make Citations Interactive

- [x] Let users inspect the exact evidence behind an answer.

**Why**

Citations are present, but they are passive. A user should be able to move from an answer to the supporting SOP section quickly, especially for operational procedures.

**How**

- Make citation rows clickable.
- Highlight or expand the matching snippet when selected.
- When line ranges become available, show or scroll to the exact supporting section.
- Keep the interaction keyboard-accessible.

**Acceptance Criteria**

- Selecting a citation clearly reveals the supporting snippet.
- Citation interaction works with keyboard navigation.
- The layout remains usable on mobile.

### 17. Add Deterministic Chunk IDs

- [x] Generate chunk IDs from stable source metadata instead of random GUIDs.

**Why**

Random chunk IDs make citations, tests, logs, and vector-store diffs harder to compare across ingests. Stable IDs make debugging and incremental ingestion much easier.

**How**

- Generate IDs from source name, section heading, chunk index, and a content hash.
- Store the content hash in metadata.
- Ensure duplicate headings still produce unique IDs.
- Update tests that assume GUID-shaped IDs.

**Acceptance Criteria**

- Re-ingesting unchanged content produces the same chunk IDs.
- Changing one section only changes IDs or hashes for affected chunks.
- Logs and citations can refer to stable chunk IDs.

### 18. Add Prompt-Injection And Grounding Tests

- [x] Test that the assistant remains grounded in the SOP under adversarial prompts.

**Why**

RAG systems are vulnerable to users asking the model to ignore instructions or invent answers. The app needs tests for refusal and grounding behavior, not only happy-path answers.

**How**

- Add fixtures for prompts such as "ignore the SOP" and "answer from your general knowledge."
- Add tests for questions whose answers are absent from the SOP.
- Verify responses say the SOP does not contain enough information.
- Verify citations are absent or clearly tied to relevant chunks.

**Acceptance Criteria**

- Prompt-injection attempts do not cause ungrounded answers.
- Out-of-scope questions get a clear "not found in SOP" style response.
- Tests run in deterministic fallback mode where possible.

### 19. Add RAG Evaluation Fixtures

- [x] Create a small repeatable evaluation set for answer quality and citation accuracy.

**Why**

Unit tests prove code behavior, but RAG quality needs scenario-level checks. A fixed question set helps detect retrieval or prompt regressions.

**How**

- Create a fixture file with representative SOP questions.
- For each question, record expected section headings or chunk IDs.
- Add an evaluation script or test mode that checks retrieved chunks and basic answer properties.
- Keep exact wording assertions loose to avoid brittle AI tests.

**Acceptance Criteria**

- Retrieval regressions are visible before manual testing.
- Evaluation can run without paid API calls for retrieval-only checks.
- Expected evidence sections are documented.

### 20. Add Embedding Cache For Ingestion

- [x] Avoid recomputing embeddings for unchanged chunks.

**Why**

If the project later supports reingestion, recomputing every embedding increases cost and latency. Stable chunk hashes make caching straightforward.

**How**

- Store chunk content hashes with vector records.
- On reingest, reuse embeddings when the content hash is unchanged.
- Only call the embedding service for new or changed chunks.

**Acceptance Criteria**

- Unchanged chunks reuse existing embeddings.
- Changed chunks are re-embedded.
- Tests verify partial reingest behavior once reingest exists.

### 21. Standardize API Error Responses

- [x] Return consistent `ProblemDetails` responses for validation, conflict, rate-limit, and server errors.

**Why**

The API currently mixes framework validation responses with custom `{ error }` payloads. Consistent error shapes make frontend handling simpler and API behavior easier to document.

**How**

- Use `ProblemDetails` for controller errors.
- Add typed error codes in `extensions`.
- Update the frontend API client to parse both current and new shapes during migration.
- Add tests for validation, conflict, rate limit, and unexpected errors.

**Acceptance Criteria**

- Error responses have a consistent shape.
- Frontend can distinguish offline, validation, conflict, rate-limit, and server errors.
- Existing user-facing error messages remain clear.

### 22. Add Request Cancellation And Timeout Handling

- [x] Make long-running chat and ingest operations easier to cancel and bound.

**Why**

OpenAI calls and ingestion can take time. Cancellation and endpoint-specific timeouts protect the server and improve user experience when requests are no longer needed.

**How**

- Use `AbortController` in the frontend for chat and ingest requests.
- Propagate cancellation tokens through backend services.
- Add endpoint-specific timeout handling where appropriate.
- Add tests around cancellation behavior for long-running service calls.

**Acceptance Criteria**

- Navigating away or retrying can cancel in-flight frontend requests.
- Backend service calls observe cancellation tokens.
- Timeout errors are reported clearly.

### 23. Improve Markdown And Link Safety

- [x] Harden assistant Markdown rendering and external links.

**Why**

`react-markdown` escapes raw HTML by default, which is good, but links still deserve explicit policy. The assistant should not create unsafe navigation surprises.

**How**

- Restrict allowed URI protocols for Markdown links.
- Consider rendering external-link indicators.
- Add tests for `javascript:` URLs and raw HTML input.
- Keep `rel="noopener noreferrer"` for external links.

**Acceptance Criteria**

- Unsafe links are not rendered as clickable navigation.
- Raw HTML is not executed.
- Markdown formatting still works for normal answers.

### 24. Improve Accessibility And Motion Preferences

- [x] Add accessibility coverage for the core chat workflow.

**Why**

The UI already has several accessibility hooks, but chat apps need careful focus handling, live-region behavior, keyboard support, and reduced-motion support.

**How**

- Respect `prefers-reduced-motion` for Framer Motion animations.
- Add tests for keyboard send, citation navigation, status dismissal, and focus after errors.
- Ensure status banners announce changes without being noisy.
- Verify color contrast for status tones and message bubbles.

**Acceptance Criteria**

- Core chat flow works with keyboard-only navigation.
- Reduced-motion users do not get unnecessary animations.
- Status and error messages are announced accessibly.

### 25. Expand CI Quality Gates

- [x] Add checks for coverage, Docker builds, formatting, and dependency vulnerabilities.

**Why**

The current CI covers backend build/test and frontend lint/test. Additional gates catch packaging, security, and production-readiness issues earlier.

**How**

- Add backend coverage reporting.
- Add frontend coverage thresholds for core files.
- Add frontend formatting check.
- Add Docker image build checks for backend and frontend.
- Add dependency scanning with Dependabot, `npm audit`, and `dotnet list package --vulnerable`.

**Acceptance Criteria**

- CI catches production image build failures.
- Dependency vulnerabilities are surfaced automatically.
- Coverage reports are available for review.

### 26. Add Container Health Checks And Hardening

- [x] Improve Docker runtime behavior and security defaults.

**Why**

The compose stack is convenient for local development, but containers should report health and avoid avoidable privilege risks.

**How**

- Add health checks for backend and frontend services.
- Run containers as non-root users where practical.
- Add `.dockerignore` files for backend and frontend.
- Add nginx configuration for SPA fallback, compression, cache headers, and security headers.
- Decide where production logs should be written so they are not trapped in ephemeral filesystems.

**Acceptance Criteria**

- `docker compose ps` reports service health.
- Docker build contexts are smaller.
- Containers avoid root where feasible.
- Frontend routes and static assets behave correctly under nginx.

### 27. Add Documentation For Contributors And Testing

- [x] Document common development workflows outside the README quick start.

**Why**

The README is already strong, but contributors benefit from targeted docs for checks, test strategy, troubleshooting, and architectural flow.

**How**

- Add `CONTRIBUTING.md` with setup, branch, test, lint, and PR expectations.
- Add `docs/testing.md` for unit, e2e, and evidence workflows.
- Add simple sequence diagrams for ingest and chat.
- Add a short tradeoffs/future-work note for challenge reviewers.

**Acceptance Criteria**

- A new contributor can find the expected local checks quickly.
- Test responsibilities are clear by layer.
- Architecture docs show the ingest and chat request flows.

## Third Wave / Platform Maturity

**Local-first constraint for every task in this section**

Any production-oriented feature must preserve the current local developer experience:

- The app must still run without external cloud services such as Azure, AWS, GCP, hosted vector databases, or managed queues.
- The app must still run without an OpenAI API key by using deterministic embeddings and fallback chat.
- The app must still run locally both with Docker Compose and without Docker.
- External providers may be added only as optional adapters behind interfaces, with JSON/file/local implementations kept as the default path.

### 28. Add Pluggable Vector Store Providers

- [x] Introduce a storage abstraction that can support production vector databases without removing the JSON vector store.

**Why**

The JSON vector store is excellent for local development and the coding challenge. A real deployment may eventually need better search performance, filtering, metadata queries, and concurrent writes.

**How**

- Keep `JsonVectorStoreService` as the default implementation.
- Define clear provider boundaries for save, load, search, delete, and metadata filtering.
- Add optional provider implementations later, such as pgvector, Qdrant, Weaviate, Azure AI Search, or another vector backend.
- Configure the provider through appsettings/environment variables.
- Make external providers opt-in only.

**Acceptance Criteria**

- Default local and Docker runs still use the JSON vector store.
- No external service is required for tests or local development.
- Provider selection is documented and validated at startup.

### 29. Support Multiple Knowledge Bases

- [x] Allow more than one SOP or knowledge base to exist at the same time.

**Why**

A real grocery chain may have different SOPs for departments, store formats, regions, or internal teams. A single global vector store will become limiting.

**How**

- Add a `knowledgeBaseId` or similar scope to ingest and chat requests.
- Store records with knowledge-base metadata.
- Keep a default knowledge base so existing local workflows still work.
- Update citations and health/readiness output to show active knowledge base information.

**Acceptance Criteria**

- Existing single-SOP flow continues to work without specifying a knowledge base.
- Multiple knowledge bases can be ingested and queried independently.
- Tests cover isolation between knowledge bases.

### 30. Add Document Versioning

- [x] Track source document versions and cite the version used for each answer.

**Why**

Operational procedures change. Users and auditors need to know which version of an SOP produced an answer.

**How**

- Store source checksum, version label, ingest timestamp, and optional uploader/admin metadata.
- Include document version metadata in vector records and citations.
- Add a default versioning strategy based on content hash when no explicit version is provided.

**Acceptance Criteria**

- Citations can identify the source document version.
- Reingesting changed content creates a distinguishable version.
- Local file-based ingestion works without any external versioning service.

### 31. Support Incremental Updates And Deletes

- [x] Update or remove changed document chunks without rebuilding the entire store.

**Why**

Once documents are versioned and chunk IDs are stable, reingesting everything becomes unnecessary and slower than needed.

**How**

- Compare incoming chunk hashes against existing records.
- Add new records, update changed records, and mark or delete removed records.
- Keep an append-only audit option for versioned history if needed.
- Preserve a simple full-rebuild path for local demos.

**Acceptance Criteria**

- Unchanged chunks remain untouched.
- Changed chunks are updated safely.
- Deleted source sections no longer appear in retrieval results.

### 32. Add Streaming Chat Responses

- [x] Stream assistant responses to the frontend instead of waiting for the full response.

**Why**

Streaming makes chat feel faster and gives users feedback during slower model calls.

**How**

- Add a streaming backend endpoint or content-negotiated mode.
- Keep the existing non-streaming endpoint for tests and simple clients.
- Render partial assistant messages in the frontend.
- Ensure fallback/no-key mode can simulate or return streaming-compatible responses locally.

**Acceptance Criteria**

- Users see assistant text appear progressively when streaming is enabled.
- Non-streaming chat still works.
- Local no-key mode still works without external services.

### 33. Add Structured AI Output

- [x] Ask the model for structured answer data instead of relying only on free-form text.

**Why**

Structured responses make it easier to connect answers to citations, confidence indicators, and UI states.

**How**

- Define a response schema with answer text, cited chunk IDs, refusal/not-found reason, and optional follow-up suggestions.
- Validate model output server-side.
- Fall back gracefully if the model returns invalid structure.
- Keep fallback chat able to produce the same response shape.

**Acceptance Criteria**

- API responses have validated structured fields.
- Citations map to explicit cited chunk IDs where possible.
- Invalid model output does not crash the request.

### 34. Add Confidence And Evidence Coverage Indicators

- [x] Surface confidence based on retrieval quality and cited evidence coverage.

**Why**

Users should know when an answer is strongly supported by the SOP versus when the system found weak or limited evidence.

**How**

- Calculate confidence from retrieval scores, number of relevant chunks, and whether cited chunks cover the answer.
- Expose a simple confidence level such as `high`, `medium`, `low`, or `not_found`.
- Display the indicator carefully in the UI without implying mathematical certainty.

**Acceptance Criteria**

- Low-confidence answers are visibly distinguishable.
- No-answer states are explicit.
- Confidence can be computed locally from retrieval metadata.

### 35. Add Query Rewriting For Follow-Up Questions

- [x] Turn conversational follow-ups into standalone retrieval queries.

**Why**

Questions like "what about Sundays?" require conversation context. Embedding only the latest short message may retrieve the wrong chunks.

**How**

- Add a query-rewrite step before retrieval.
- Use recent conversation history to produce a standalone search query.
- In fallback mode, use a simple deterministic heuristic.
- Log both original and rewritten query metadata without logging sensitive full content by default.

**Acceptance Criteria**

- Follow-up questions retrieve relevant SOP sections more reliably.
- Rewriting can be disabled by configuration.
- Local no-key mode remains functional.

### 36. Add Reranking

- [x] Add an optional reranking stage after initial vector retrieval.

**Why**

Vector search can retrieve broadly relevant chunks but rank exact procedural evidence lower than desired. Reranking can improve final context quality.

**How**

- Retrieve a larger candidate set.
- Rerank candidates with a local lexical score by default.
- Optionally support model-based or provider-based rerankers behind an interface.
- Keep the local lexical reranker as the default implementation.

**Acceptance Criteria**

- Retrieval quality improves or remains stable in evaluation fixtures.
- No external reranking service is required.
- Reranking can be turned off by configuration.

### 37. Add AI Safety And Escalation Guardrails

- [x] Define behavior for topics the assistant should not handle directly.

**Why**

An internal SOP assistant may receive HR, medical, legal, emergency, or safety-critical questions. Some should be answered only from SOP content, and others should direct the user to a manager or official channel.

**How**

- Add configurable guardrail categories and canned escalation language.
- Test out-of-scope and high-risk prompts.
- Keep guardrails local and rule-based by default.
- Make model-based guardrails optional only.

**Acceptance Criteria**

- High-risk unsupported questions do not receive invented advice.
- Escalation language is clear and consistent.
- Guardrail behavior works in fallback mode.

### 38. Add Token And Cost Tracking

- [x] Track token usage and estimated cost for OpenAI-backed requests.

**Why**

AI features can become expensive. Cost and token observability help tune prompts, chunking, and retrieval limits.

**How**

- Capture prompt, completion, and embedding token usage where SDK responses expose it.
- Add configurable per-model pricing metadata.
- Log estimated cost per request without exposing secrets.
- In fallback mode, report zero external cost and optional estimated local token counts.

**Acceptance Criteria**

- OpenAI-backed requests produce usage/cost telemetry.
- Fallback mode clearly reports no external API cost.
- Cost tracking does not block requests if usage metadata is missing.

### 39. Add Upload History And Ingestion Audit Records

- [x] Record what was ingested, when, by whom or what process, and with which outcome.

**Why**

Ingestion changes the knowledge base. Even local tools benefit from an audit trail when debugging why an answer changed.

**How**

- Store audit records in a local JSON file by default.
- Include source name, checksum, version, chunk count, success/failure, and timestamp.
- Add optional user/admin identity fields for future auth.
- Keep external audit sinks optional adapters only.

**Acceptance Criteria**

- Local runs produce readable ingestion audit records.
- Failed ingests are recorded with safe error summaries.
- Audit records do not require a database or cloud service.

### 40. Support Additional Document Formats

- [x] Add ingestion support for PDF, DOCX, and scanned documents through optional local processors.

**Why**

SOPs often exist outside Markdown. Supporting common document formats would make the app more realistic.

**How**

- Add a document extraction abstraction.
- Keep Markdown and plain text as the default baseline.
- Add PDF/DOCX extraction using local libraries where possible.
- Treat OCR as optional because it may require heavier local dependencies.
- Preserve the current upload limits and validation.

**Acceptance Criteria**

- Markdown and text ingestion continue to work unchanged.
- PDF/DOCX support works locally without cloud services.
- OCR, if added, is optional and documented separately.

### 41. Add Duplicate Document Detection

- [x] Detect when an uploaded or configured document has already been ingested.

**Why**

Duplicate ingestion wastes storage, tokens, and reviewer time. It can also clutter version history.

**How**

- Calculate a source checksum before chunking.
- Compare against ingestion audit records or stored document metadata.
- Return a clear already-ingested response when the checksum matches.

**Acceptance Criteria**

- Identical documents are detected before embedding work starts.
- The response explains which document/version already exists.
- Detection works with local JSON metadata.

### 42. Add Ingestion Preview

- [x] Let users preview chunks before committing ingestion.

**Why**

Chunking quality has a direct effect on answer quality. A preview makes it easier to spot bad extraction or chunk boundaries.

**How**

- Add a preview endpoint that chunks but does not embed or save.
- Show chunk count, headings, approximate size, and sample text.
- Add a commit step that performs the actual ingest.

**Acceptance Criteria**

- Preview does not mutate the vector store.
- Users can inspect chunks before ingestion.
- Existing one-click default ingest can remain available for demos.

### 43. Add Authentication And Authorization

- [x] Protect admin operations while preserving local no-auth development.

**Why**

Reset, reingest, upload, and future audit views should not be open in production-like environments.

**How**

- Add an auth abstraction and policy names for admin/user actions.
- Keep a local development mode that allows access without external identity providers.
- Support simple local API-key auth before considering enterprise providers.
- Make Azure AD, AWS Cognito, or other identity providers optional adapters only.

**Acceptance Criteria**

- Local development still works without setting up an identity provider.
- Admin endpoints can be protected by configuration.
- Tests cover authorized, unauthorized, and local-dev behavior.

### 44. Add User Roles

- [x] Distinguish regular chat users from operators/admins.

**Why**

Most employees should ask questions, while only specific users should upload, reset, or manage documents.

**How**

- Define role policies such as `ChatUser`, `KnowledgeAdmin`, and `Operator`.
- Apply policies to ingest/reset/audit endpoints.
- Keep role assignment simple and local-configurable for development.

**Acceptance Criteria**

- Chat remains available to normal users.
- Knowledge-base mutation requires an admin role outside local permissive mode.
- Role behavior is covered by tests.

### 45. Add Distributed Rate Limiting Option

- [x] Support rate limiting across multiple backend instances without requiring it locally.

**Why**

In-memory rate limiting works for one process. Multiple instances need shared counters to enforce limits consistently.

**How**

- Keep current in-memory rate limiting as the default.
- Add an interface or configuration path for distributed rate-limit stores.
- Support optional Redis or database-backed counters later.
- Ensure local and Docker Compose runs do not require Redis.

**Acceptance Criteria**

- Single-instance local behavior is unchanged.
- Distributed mode is opt-in.
- Missing distributed dependencies fail clearly only when that mode is selected.

### 46. Add OpenTelemetry Tracing And Metrics

- [x] Instrument requests, retrieval, ingestion, vector search, and model calls.

**Why**

Tracing and metrics make latency and failure analysis much easier as the system grows.

**How**

- Add OpenTelemetry instrumentation for ASP.NET Core.
- Create spans for chunking, embedding, vector search, chat completion, and tool calls.
- Export to console or local OTLP only by default.
- Make cloud observability backends optional.

**Acceptance Criteria**

- Local runs can emit traces/metrics without cloud accounts.
- External observability exporters are opt-in.
- Instrumentation does not log secrets or raw document content by default.

### 47. Add Circuit Breaker For OpenAI Calls

- [x] Stop repeatedly calling OpenAI during provider instability.

**Why**

Retries help with transient errors, but persistent failures should trip a circuit breaker to reduce latency, cost, and pressure on the provider.

**How**

- Extend the Polly pipeline with a circuit breaker.
- Add clear degraded-mode logging.
- Consider falling back to deterministic/local behavior only when safe and explicit.

**Acceptance Criteria**

- Repeated provider failures open the circuit.
- Requests fail fast or degrade clearly while the circuit is open.
- No-key fallback mode remains unaffected.

### 48. Move Ingestion To A Background Job

- [x] Avoid doing long ingestion work entirely inside the HTTP request.

**Why**

Large documents, extra formats, and embedding calls can exceed normal request lifetimes. A background job model gives better status reporting and cancellation.

**How**

- Add a local in-process background queue as the default.
- Return a job ID from ingest requests.
- Add job status endpoint.
- Keep a synchronous ingest path or test mode for simple local/e2e workflows.
- Make external queues optional adapters only.

**Acceptance Criteria**

- Ingestion status can be queried.
- Local development does not require Redis, SQS, Azure Queue, or any external queue.
- Existing simple ingest tests can still run deterministically.

### 49. Add Conversation Feedback

- [x] Let users mark answers as helpful, unhelpful, or wrong-citation.

**Why**

Feedback is useful for improving prompts, retrieval settings, and source documents.

**How**

- Add feedback buttons on assistant messages.
- Store feedback locally in JSON by default.
- Include conversation ID, message ID, selected feedback, optional comment, and timestamp.
- Avoid storing raw conversation content unless explicitly configured.

**Acceptance Criteria**

- Users can submit feedback from the chat UI.
- Feedback persists locally without external services.
- Feedback records avoid unnecessary sensitive content.

### 50. Add Source Document Viewer

- [x] Provide a way to inspect SOP content alongside answers.

**Why**

Operational users may want to verify the surrounding context, not just a citation snippet.

**How**

- Add a document viewer panel for ingested sources.
- Start with local Markdown/text rendering.
- Link citations to the relevant section or line range.
- Keep PDF/DOCX viewing optional for later format support.

**Acceptance Criteria**

- Users can view the relevant source section from a citation.
- Markdown/text documents render locally.
- Chat remains usable on smaller screens.

### 51. Generate TypeScript API Types From OpenAPI

- [x] Reduce drift between backend DTOs and frontend TypeScript types.

**Why**

Manual frontend types can drift from backend contracts. Generated types make API changes safer.

**How**

- Generate OpenAPI during backend build or CI.
- Use a local codegen tool to produce frontend API types.
- Keep generated files deterministic and checked in only if that matches the repo workflow.
- Ensure generation works offline after dependencies are installed.

**Acceptance Criteria**

- Frontend request/response types match backend OpenAPI.
- CI catches contract drift.
- Local development can regenerate types without external services.

### 52. Add Contract Tests

- [x] Validate frontend/backend API compatibility beyond unit tests.

**Why**

Even with generated types, runtime shapes and error responses need coverage.

**How**

- Add tests that compare backend OpenAPI output to expected client assumptions.
- Validate representative request/response payloads.
- Cover error response shapes.

**Acceptance Criteria**

- Contract drift fails CI.
- Error payload contracts are tested.
- Tests run locally without external services.

### 53. Add Visual Regression Tests

- [x] Catch unintended UI changes in the chat, setup, and citation flows.

**Why**

The app is visual and evidence-driven. Screenshots help catch layout regressions that unit tests miss.

**How**

- Add Playwright screenshot assertions for stable states.
- Run against fallback mode and deterministic data.
- Store/update baselines intentionally.

**Acceptance Criteria**

- Major layout regressions fail tests.
- Tests are deterministic enough for CI.
- Baselines are documented.

### 54. Add Load And Resilience Tests

- [x] Test behavior under concurrent chat, ingest, timeout, and provider-failure scenarios.

**Why**

Concurrency and provider errors are where many RAG apps fail in surprising ways.

**How**

- Add local load tests for chat and ingest endpoints.
- Simulate OpenAI timeout, rate-limit, and server-error responses.
- Test vector-store locking and rate limiting under concurrency.
- Keep tests local and deterministic with fake services.

**Acceptance Criteria**

- Concurrency behavior is measured and repeatable.
- Provider-failure paths are covered.
- Tests do not call external APIs.

### 55. Add Supply Chain Security Checks

- [ ] Add secret scanning, SBOM generation, image vulnerability scanning, and license checks.

**Why**

Dependencies and containers are part of the delivered system. Supply-chain checks help catch vulnerabilities and accidental secret exposure early.

**How**

- Add secret scanning in CI.
- Generate SBOMs for backend and frontend images.
- Scan container images for vulnerabilities.
- Add dependency license checks.
- Keep checks local/CI-tool based without requiring cloud accounts.

**Acceptance Criteria**

- CI surfaces vulnerable packages, risky licenses, and accidental secrets.
- Image scans run on built images.
- Checks can run in public CI without external paid services.

### 56. Add Uploaded-File Content Validation

- [ ] Validate uploaded file content, not only filename extensions.

**Why**

Extensions can be misleading. Content validation reduces accidental or malicious uploads.

**How**

- Check MIME type where reliable, file signatures where applicable, and text decodability.
- Reject binary or suspicious content for Markdown/text ingestion.
- Add size and line-length safeguards.
- Keep malware scanning as an optional future hook, not a local requirement.

**Acceptance Criteria**

- Renamed binary files are rejected.
- Valid Markdown/text files still ingest normally.
- Validation works locally without external scanners.

### 57. Add Data Retention Controls

- [ ] Define how long logs, uploads, vector records, chat history, feedback, and audit records are retained.

**Why**

Even internal tools should avoid retaining data forever by accident.

**How**

- Add configurable retention settings.
- Provide local cleanup commands or scripts.
- Document what data is stored and where.
- Keep defaults friendly for local demos.

**Acceptance Criteria**

- Stored data locations and retention defaults are documented.
- Cleanup can run locally.
- Retention does not delete active demo data unexpectedly.

### 58. Add Challenge Review Notes And Roadmap

- [ ] Add a concise reviewer-facing document explaining tradeoffs and future roadmap.

**Why**

This project is a coding challenge. Reviewers benefit from knowing which decisions were intentional and which improvements are planned.

**How**

- Add `docs/review-notes.md`.
- Summarize local-first design, fallback mode, JSON vector store tradeoffs, RAG limitations, testing strategy, and roadmap.
- Link to this TODO file for detailed backlog.

**Acceptance Criteria**

- Reviewers can quickly understand architectural intent.
- Local/no-cloud constraints are explicit.
- The roadmap is easy to find.

## Implementation Standards / Architecture Guidance

**Free and local-first tooling constraint**

All future implementation work must use only:

- Built-in .NET / ASP.NET Core capabilities.
- Built-in React / TypeScript / Vite capabilities where practical.
- Free and open-source packages with licenses suitable for commercial/internal use, such as MIT, Apache-2.0, BSD, or similarly permissive licenses.

Avoid packages that require paid licenses for commercial use or have unclear licensing. Do not introduce paid tooling such as commercial mappers, paid UI kits, paid observability SaaS SDK lock-in, or cloud-only services as required dependencies. If a paid or hosted provider is useful, it may be mentioned only as an optional adapter, never as the default path.

### 59. Add Dependency License Policy

- [ ] Document and enforce allowed dependency licenses for backend, frontend, test, and Docker tooling.

**Why**

The project should remain free to run and maintain. Some libraries that were historically common in .NET ecosystems have changed licensing or may not be free for all commercial use cases.

**How**

- Add a dependency policy to `CONTRIBUTING.md` or `docs/dependency-policy.md`.
- Prefer built-in .NET/React features before adding libraries.
- Require license review for new dependencies.
- Avoid paid/commercial-license libraries such as paid mappers or paid UI component suites.
- Add CI checks for package licenses where practical.

**Acceptance Criteria**

- New dependencies must have acceptable licenses.
- CI or contributor docs make license review explicit.
- The app remains runnable without paid tooling or accounts.

### 60. Review .NET 10 Usage And Modern ASP.NET Core Patterns

- [ ] Audit backend code for idiomatic .NET 10 / ASP.NET Core usage.

**Why**

The project already uses .NET 10. A focused review can make sure it benefits from current platform features instead of carrying older patterns unnecessarily.

**How**

- Review middleware ordering, configuration binding, validation, rate limiting, health checks, exception handling, and OpenAPI generation.
- Prefer built-in .NET features before third-party packages.
- Use keyed services or options patterns where they simplify provider selection.
- Keep nullable reference types and async/cancellation behavior clean.

**Acceptance Criteria**

- Backend startup and service registration are idiomatic and easy to follow.
- Configuration is strongly typed where useful.
- New patterns do not break local/no-Docker or Docker workflows.

### 61. Evaluate Minimal API For Endpoint Design

- [ ] Decide whether controllers should remain or whether selected endpoints should move to Minimal API route groups.

**Why**

Minimal APIs can reduce ceremony for small services and pair well with typed results, endpoint filters, route groups, and OpenAPI metadata. Controllers are also fine when they are clearer. The project should choose intentionally.

**How**

- Compare current controllers with Minimal API route groups for `health`, `ingest`, and `chat`.
- Prefer Minimal API only if it improves readability, validation, typed responses, and testability.
- Keep API versioning and OpenAPI output intact.
- Avoid mixed patterns unless there is a clear boundary.

**Acceptance Criteria**

- The chosen endpoint style is documented in ADRs.
- Endpoint tests remain clear.
- OpenAPI docs remain accurate.
- No external packages are required for the endpoint style.

### 62. Add Mediator-Style Application Layer Without Paid Dependencies

- [ ] Introduce request/handler boundaries where they reduce controller/service coupling.

**Why**

As features grow, controllers can become orchestration-heavy. A mediator-style application layer can organize commands and queries such as `IngestDocument`, `SendChatMessage`, `ResetKnowledgeBase`, and `GetReadiness`.

**How**

- Prefer a small in-house dispatcher or direct command/query handler interfaces before adding a package.
- If using a package, choose a free/open-source option with acceptable licensing.
- Do not use paid mediator/commercial framework features.
- Keep handlers easy to unit test.
- Avoid adding mediator abstractions where a direct service call is clearer.

**Acceptance Criteria**

- Application workflows are organized into testable handlers.
- No paid mediator package is required.
- The added abstraction reduces complexity instead of adding ceremony.

### 63. Avoid AutoMapper And Prefer Explicit Mapping

- [ ] Use explicit mapping methods for DTO/domain transformations.

**Why**

Explicit mapping is simple, transparent, easy to debug, and avoids licensing risk from mapper packages. It also makes API contract changes easier to review.

**How**

- Add small static mapper methods or extension methods where mapping duplication appears.
- Keep mapping close to the application boundary.
- Avoid reflection-heavy or convention-only mapping for API contracts.
- Do not add AutoMapper or other paid/commercial mappers.

**Acceptance Criteria**

- DTO mappings are visible in code.
- No paid mapper dependency is introduced.
- Tests cover non-trivial mappings.

### 64. Add SignalR Only Where Real-Time UX Justifies It

- [ ] Evaluate SignalR for streaming, job status, and multi-user updates while keeping simpler paths available.

**Why**

SignalR is a built-in ASP.NET Core option for real-time updates. It may help with streaming chat, ingestion job progress, or admin dashboards, but it should not be added just for novelty.

**How**

- Compare SignalR with Server-Sent Events, fetch streaming, and polling.
- Use SignalR only if bidirectional real-time communication is truly useful.
- Keep a non-SignalR fallback for local/e2e tests and simple deployments.
- Do not require Azure SignalR or any managed real-time service.

**Acceptance Criteria**

- Real-time behavior works locally without cloud services.
- Docker and non-Docker local runs still work.
- Simpler HTTP/polling paths remain available where appropriate.

### 65. Strengthen Observability Plan

- [ ] Make logs, metrics, and traces useful without leaking sensitive content.

**Why**

Good observability is not just "more logs." It should answer operational questions while avoiding raw SOP content, full user prompts, API keys, or unnecessary personal data.

**How**

- Define structured log fields for request ID, conversation ID, knowledge base ID, document version, model, provider mode, retrieval scores, chunk IDs, duration, status, and error code.
- Use logging scopes for request/conversation context.
- Keep raw prompt/document logging disabled by default.
- Add metrics for request counts, latency, chat failures, ingest failures, rate-limit hits, vector search duration, and OpenAI call duration.
- Add OpenTelemetry console/local exporters by default and make hosted exporters optional.

**Acceptance Criteria**

- Logs are structured and consistent.
- Sensitive data is not logged by default.
- Local runs can inspect logs/traces without cloud accounts.

### 66. Define Health, Readiness, And Startup Checks

- [ ] Formalize operational checks for liveness, readiness, and startup validation.

**Why**

Health endpoints should be meaningful for humans, Docker, and deployment tools. Liveness should say the process is running; readiness should say the app can serve useful requests.

**How**

- Keep a lightweight liveness endpoint.
- Add readiness checks for vector-store path access, source document availability, active AI mode, configuration validity, and optional provider connectivity only when those providers are enabled.
- Add startup validation for required config values in the selected mode.
- Wire Docker health checks to the appropriate endpoint.

**Acceptance Criteria**

- Local and Docker health checks work without external services.
- Optional external provider checks run only when those providers are selected.
- Health/readiness output is useful but does not leak secrets.

### 67. Establish Backend Design Patterns

- [ ] Document preferred backend patterns so future changes stay consistent.

**Why**

The project will be easier to maintain if it has clear boundaries for API contracts, application workflows, domain models, infrastructure adapters, and cross-cutting concerns.

**How**

- Define folders/namespaces for contracts, application handlers, domain models, services, infrastructure adapters, and options.
- Keep controllers or Minimal API endpoints thin.
- Put business workflow orchestration in application handlers/services.
- Keep provider-specific logic behind interfaces.
- Prefer constructor injection, options validation, explicit mapping, and small focused services.

**Acceptance Criteria**

- New features have an obvious place to live.
- Provider-specific code does not leak into controllers/endpoints.
- Architecture guidance is captured in an ADR or contributor doc.

### 68. Establish Frontend Design Patterns

- [ ] Document preferred frontend structure for state, API calls, components, and UX behavior.

**Why**

React code can become tangled as chat features grow. Clear conventions make it easier to add streaming, citations, feedback, and admin views.

**How**

- Keep API access in typed service modules.
- Keep reusable UI pieces in components and workflow state in page/hooks.
- Introduce custom hooks when stateful behavior repeats.
- Prefer React built-ins before adding state-management libraries.
- Keep accessibility, reduced-motion, and error states part of component design.

**Acceptance Criteria**

- Frontend feature additions follow consistent boundaries.
- No paid UI kit or paid state-management tooling is introduced.
- Component tests cover important user behavior.

### 69. Define Logging Levels And Message Guidelines

- [ ] Standardize what belongs at debug, information, warning, error, and critical levels.

**Why**

Appropriate logging means important events stand out and normal operation does not look noisy or broken.

**How**

- Add guidelines for each log level.
- Use `Information` for major successful operations, `Warning` for degraded/retry/low-confidence conditions, and `Error` for failed operations requiring attention.
- Include error codes and correlation fields.
- Avoid logging stack traces for expected validation or conflict responses.

**Acceptance Criteria**

- Logs are quiet during normal happy-path usage.
- Failures include enough context to debug.
- Expected user errors do not appear as server errors.

### 70. Add Local Developer Observability Profile

- [ ] Make local debugging pleasant without external observability tools.

**Why**

The project should remain easy to understand on a laptop. Developers should not need Datadog, Application Insights, CloudWatch, or any paid service to diagnose behavior.

**How**

- Add a local logging profile with readable console logs.
- Optionally add local OpenTelemetry console exporter.
- Document how to inspect logs in local process mode and Docker mode.
- Keep file logging configurable so it does not surprise container users.

**Acceptance Criteria**

- Developers can debug chat/ingest behavior locally.
- Docker logs show useful structured messages.
- No paid observability account is needed.
