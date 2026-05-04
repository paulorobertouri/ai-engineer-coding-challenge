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
- **No runtime dependencies:** No SQLite driver, native extension, or database engine required.

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

