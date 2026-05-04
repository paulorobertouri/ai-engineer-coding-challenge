# Backend

.NET 10 Web API for the Grocery Store SOP Assistant.

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/health` | Health check |
| `POST` | `/api/ingest` | Chunk, embed, and store the SOP document |
| `POST` | `/api/chat` | RAG-grounded chat with tool-calling support |

## Services

| Interface | Implementation | Description |
|---|---|---|
| `IChunkingService` | `MarkdownChunkingService` | Splits markdown by `#` headers into semantic chunks |
| `IEmbeddingService` | `OpenAIEmbeddingService` | Generates embeddings via OpenAI `text-embedding-3-small` |
| `IVectorStoreService` | `JsonVectorStoreService` | Persists and queries vectors using a JSON file (`vector-store.json`) with in-memory cosine similarity |
| `IRetrievalChatService` | `OpenAIRetrievalChatService` | RAG pipeline with Polly resilience (retries + timeout) |
| `IToolRegistryService` | — | Registers `get_store_hours` and `search_sop` tools for the agent |

## Vector Store

JSON file stored at `Data/vector-store.json` (configurable via `Challenge__VectorStorePath`).  
Inside Docker the file lives at `/app/Data/vector-store.json` and is mounted from `./backend/src/Api/Data`.

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
dotnet test backend/src/Api.Tests/Api.Tests.csproj -c Release
```

## Formatting

```bash
# check only
./scripts/format.sh

# auto-fix
./scripts/format.sh --fix
```
