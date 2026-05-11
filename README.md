# Grocery Store SOP Assistant — Vertical Slice

A Proof of Concept internal chatbot for a grocery store chain. Employees ask questions about operating procedures and receive answers grounded in the SOP document, with source citations.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 Web API (C#) |
| Frontend | React 19 + TypeScript + Vite |
| AI | OpenAI SDK for .NET — `gpt-4o-mini` (chat), `text-embedding-3-small` (embeddings) |
| Vector Store | JSON file + in-memory cosine similarity (no external DB) |
| Resilience | Polly — exponential backoff, 3 retries, 30 s timeout |
| Validation | Data Annotations (backend) · Zod (frontend) |
| Containerisation | Docker & Docker Compose |
| CI | GitHub Actions |
| Testing | xUnit (backend) · Vitest (frontend) · Playwright (e2e) |

## Quick Start

```bash
# 1. Install dependencies and copy .env files
./scripts/setup.sh

# 2. Add your OpenAI API key
#    Open .env and set OpenAI__ApiKey=<your-key>

# 3. Build and start the full stack
./scripts/build.sh
docker compose up -d
```

## Configuration

The project uses a central `.env` file at the root for configuration and secret management.

1. **Create your `.env` file** (done automatically by `setup.sh`):
   ```bash
   cp .env.example .env
   ```
2. **Set your OpenAI API Key:** Open `.env` and fill in `OpenAI__ApiKey`.

All services (backend, frontend, Docker Compose) are pre-configured to read from this central file.

> **Offline / no-key mode:** If `OpenAI__ApiKey` is empty the backend automatically falls back to `DeterministicEmbeddingService` (FNV1a hashing) and `FallbackRetrievalChatService` (hard-coded store-hours + keyword matching), so the app remains runnable without an API key.

## Scripts

| Script | Purpose |
|---|---|
| `./scripts/setup.sh` | Copy `.env` files, `npm ci`, `dotnet restore` |
| `./scripts/format.sh [--fix]` | Check / auto-fix formatting and linting |
| `./scripts/test.sh [all\|backend\|frontend]` | Run unit tests |
| `./scripts/build.sh [all\|backend\|frontend]` | Build Docker images |
| `./scripts/docker.sh <up\|down\|restart\|logs\|status> [service]` | Manage the Docker Compose stack |
| `./scripts/e2e.sh [test\|evidence]` | Run Playwright e2e tests or capture evidence screenshots |

## Implemented Features

### 1. Document Ingestion
- `POST /api/v1/ingest` reads the SOP document from the server-side configured path (`Challenge__SourceDocumentPath`). The source path is never accepted from the client (OWASP A01 path-traversal prevention).
- `MarkdownChunkingService` splits the document on level-1 (`#`) and level-2 (`##`) headers, producing one semantic chunk per section.
- Each chunk is embedded with `text-embedding-3-small` and written to `Data/vector-store.json` as a JSON array of `VectorRecord` objects.
- Path resolution supports absolute paths, paths relative to `ContentRoot`, and a Docker-aware fallback.

### 2. RAG (Retrieval-Augmented Generation)
- On every chat turn the latest user message is embedded and the top-3 chunks are retrieved by cosine similarity.
- Retrieved chunks are injected into the OpenAI system prompt so the model answers only from SOP content.
- The response includes a `citations` array (`source`, `snippet`) mapping each answer back to its source chunk.

### 3. Tool Calling (Single-Turn Agent Behaviour)
The chat service defines two OpenAI function tools and lets the model decide when to invoke them:

| Tool | Schema | Behaviour |
|---|---|---|
| `search_sop` | `{ query: string }` | Re-embeds the query and retrieves 3 more chunks from the vector store |

When the model returns `finish_reason: tool_calls`, each tool is executed and its result is appended as a `tool` message; the model is then called a second time to produce the final response.

### 4. Multi-Turn Chat
- `ChatRequest.Messages` carries the full conversation history on every call — stateless server, client owns the state.
- `ChatMessageDto` is validated with `[RegularExpression("^(user|assistant|system)$")]` to prevent injection via unsupported roles.

### 5. Resilience & Observability
- Polly pipeline: exponential-backoff retry (×3, 2 s base) + 30 s timeout wraps every OpenAI call.
- Serilog structured logging with daily rolling file sink (`Logs/api-YYYYMMDD.log`) and console output.
- `GlobalExceptionHandler` maps unhandled exceptions to RFC 9457 `ProblemDetails` responses.
- Response compression (Gzip / Brotli) enabled for HTTPS.
- Forwarded headers middleware (`X-Forwarded-For`, `X-Forwarded-Proto`) for accurate client IP detection behind proxies/Docker.

### 6. Rate Limiting
Fixed-window rate limiting applied per client IP:

| Endpoint | Policy | Limit |
|---|---|---|
| `POST /api/v1/chat` | `chat` | 30 requests / minute |
| `POST /api/v1/ingest` | `ingest` | 10 requests / minute |

Exceeding the limit returns `429 Too Many Requests`.

### 7. Security Headers
`X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `X-XSS-Protection: 1; mode=block`, `Referrer-Policy: strict-origin-when-cross-origin`, HSTS (non-development).

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/health` | Returns service name, UTC time, and mode-aware operational notes |
| `POST` | `/api/v1/ingest` | Chunk → embed → persist the SOP document |
| `POST` | `/api/v1/chat` | RAG chat with optional tool-calling |

## How to Run

### Docker (Recommended)
```bash
./scripts/setup.sh          # first-time setup
./scripts/docker.sh up      # build images and start stack
./scripts/docker.sh down    # stop stack
./scripts/docker.sh restart # rebuild and restart
./scripts/docker.sh logs    # tail logs (Ctrl-C to exit)
```

Scope to a single service: `./scripts/docker.sh up backend`

| Service | URL |
|---|---|
| Backend API | `http://localhost:5181` |
| Frontend | `http://localhost:5173` |

### Local Development
1. **Backend:**
   ```bash
   cd backend/src/Api
   dotnet run
   ```
2. **Frontend:**
   ```bash
   cd frontend
   npm run dev
   ```

### Testing
```bash
./scripts/test.sh            # all unit tests
./scripts/test.sh backend    # dotnet test only
./scripts/test.sh frontend   # vitest only
```

### Format & Lint
```bash
./scripts/format.sh          # check only
./scripts/format.sh --fix    # auto-fix (backend + frontend + e2e)
```

### E2E Tests & Evidence
```bash
./scripts/e2e.sh             # run all Playwright e2e tests
./scripts/e2e.sh evidence    # capture evidence screenshots → evidences/
```

## Visual Evidence

Screenshots captured from the running application:

![Initial load](evidences/01-initial-load.png)

![After ingest](evidences/02-after-ingest.png)

![Chat response — SOP question](evidences/03-chat-response-1.png)

![Chat response — store hours via tool call](evidences/04-chat-response-2-hours.png)

## Key Design Decisions

- **Markdown chunking by `#`/`##` headers:** Splits on both level-1 and level-2 headers so top-level document titles form their own chunk and each SOP section (e.g. "Opening Procedures") is a single coherent unit.
- **JSON vector store:** Avoids native runtime dependencies; the entire store is a single portable file, trivial to inspect and reset.
- **Stateless chat server:** The client sends a sliding window of up to the last 20 messages on every request. This eliminates server-side session state and caps token growth for long conversations.
- **Conditional service registration:** The DI container wires real OpenAI services (including `OpenAIClient`) only when a key is present, and deterministic fallbacks otherwise. The health endpoint dynamically reflects the active mode in its response notes.
- **Polly over custom retry logic:** Standardised resilience with observable retry events; easy to extend with circuit-breaker or hedging policies later.

See [docs/adr/ARCH_DECISIONS.md](docs/adr/ARCH_DECISIONS.md) for full architectural decision records.
