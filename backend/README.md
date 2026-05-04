# Backend

.NET 10 Web API for the Grocery Store SOP Assistant.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/health` | Returns service name, UTC time, and operational notes |
| `POST` | `/api/ingest` | Chunk → embed → persist the SOP document |
| `POST` | `/api/chat` | RAG-grounded multi-turn chat with tool-calling support |

## Services

| Interface | Production Implementation | Fallback (no API key) | Description |
|---|---|---|---|
| `IChunkingService` | `MarkdownChunkingService` | _(same)_ | Splits markdown on `##` level-2 headers into semantic chunks |
| `IEmbeddingService` | `OpenAIEmbeddingService` | `DeterministicEmbeddingService` | Generates embeddings via `text-embedding-3-small`; fallback uses FNV1a hashing |
| `IVectorStoreService` | `JsonVectorStoreService` | _(same)_ | In-memory cosine similarity search backed by `Data/vector-store.json` |
| `IRetrievalChatService` | `OpenAIRetrievalChatService` | `FallbackRetrievalChatService` | RAG pipeline with Polly resilience; fallback uses keyword matching |

Service registration in `Program.cs` is conditional: when `OpenAI:ApiKey` is present the real OpenAI services are wired; otherwise the deterministic fallbacks are used, keeping the app runnable without an API key.

## Tool Calling

`OpenAIRetrievalChatService` defines two OpenAI function tools:

| Tool | Parameters | Behaviour |
|---|---|---|
| `get_store_hours` | _(none)_ | Returns hard-coded Mon–Sun operating hours |
| `search_sop` | `query: string` | Re-embeds the query and retrieves 3 chunks from the vector store |

When `finish_reason` is `tool_calls` each tool is executed, its result appended as a `tool` message, and the model called a second time to produce the final response.

## Vector Store

- File: `Data/vector-store.json` (configurable via `Challenge__VectorStorePath`)
- Inside Docker the file lives at `/app/Data/vector-store.json` (mounted from `./backend/src/Api/Data`)
- Format: JSON array of `VectorRecord` objects (`id`, `source`, `chunkText`, `embedding`, `metadata`)
- Search: cosine similarity computed in-process over all records (suitable for POC scale)

## Resilience

`OpenAIRetrievalChatService` wraps every OpenAI call in a Polly pipeline:
- Exponential backoff retry — 3 attempts, 2 s base delay
- 30 s timeout

## Configuration

| Key | Default | Description |
|---|---|---|
| `OpenAI:ApiKey` | _(empty)_ | OpenAI API key — triggers fallback mode if absent |
| `OpenAI:ChatModel` | `gpt-4o-mini` | Chat completion model |
| `OpenAI:EmbeddingModel` | `text-embedding-3-small` | Embedding model |
| `Challenge:SourceDocumentPath` | `Data/Grocery_Store_SOP.md` | Path to the SOP markdown file |
| `Challenge:VectorStorePath` | `Data/vector-store.json` | Path for vector store persistence |
| `Cors:AllowedOrigins` | `["http://localhost:5173"]` | Allowed CORS origins |

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

Test coverage:
- `MarkdownChunkingServiceTests` — verifies chunking by `##` headers and empty-input handling
- `SqliteVectorStoreServiceTests` — verifies cosine similarity ordering, missing-file graceful load, and save/load round-trip

## Formatting

```bash
# check only
./scripts/format.sh

# auto-fix
./scripts/format.sh --fix
```
