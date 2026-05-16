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

