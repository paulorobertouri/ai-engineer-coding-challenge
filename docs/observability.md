# Observability Plan

This project keeps observability local-first by default and avoids logging sensitive user data.

## Principles

- Prefer structured logs with stable field names.
- Avoid raw user prompt and source-document content in logs.
- Keep traces and metrics usable in local runs without cloud accounts.
- Make hosted exporters optional through configuration.

## Logging Fields

Include these fields whenever available:

- `TraceId`
- `ConversationId`
- `KnowledgeBaseId`
- `ProviderMode` (`fallback` or `openai`)
- `Model`
- `Status` / error code
- Duration values (`CompletionLatencyMs`, `TotalLatencyMs`)

Use logging scopes for request and conversation context in service flows.

## Sensitive Data Defaults

By default, do not log:

- raw user prompts
- full SOP/source document content
- API keys, tokens, or secrets
- free-form tool-call query text

Use lengths, identifiers, and aggregate counters instead of raw content.

## Metrics

Current local metrics include:

- `chat.requests`
- `chat.latency.ms`
- `chat.failures`
- `vector.search.latency.ms`
- `openai.call.latency.ms`
- `ingest.chunks`
- `ingest.latency.ms`
- `ingest.failures`
- `http.rate_limit.rejections`

## Tracing

Activity source:

- `GroceryStore.SopAssistant`

Key spans:

- `chat.fallback.generate`
- `chat.openai.generate`
- `vector.search`
- `vector.search.filtered`
- `ingest.process`

## Local-First Runtime

Observability settings are configured via `Observability` options:

- `Enabled`: toggle telemetry pipeline
- `EnableConsoleExporter`: emit traces/metrics locally
- `OtlpEndpoint`: optional hosted exporter endpoint

With console exporter enabled, local development can inspect metrics/traces without external services.
