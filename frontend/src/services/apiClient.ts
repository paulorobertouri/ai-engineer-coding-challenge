import type {
  ChatRequest,
  ChatResponse,
  ConversationFeedbackRequest,
  ConversationFeedbackResponse,
  HealthResponse,
  IngestJobStatusResponse,
  IngestRequest,
  IngestResponse,
  SourceDeleteResponse,
  SourceListItem,
  SourceDocumentResponse,
  SourceComparisonResponse,
  SourceUpdateAlertResponse,
  SourceQualityReportResponse,
  OperatorAuditDashboardResponse,
  RetrievalBenchmarkDashboardResponse,
  RetrievalBenchmarkEntry,
} from '../types/chat'

interface RuntimeConfig {
  apiBaseUrl?: string
}

interface LegacyErrorBody {
  error?: string
}

interface ProblemDetailsBody {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  code?: string
  extensions?: {
    code?: string
  }
}

interface ChatStreamDeltaEvent {
  delta?: string
}

const STREAM_READ_TIMEOUT_MS = 15_000

type ApiErrorCode =
  | 'validation_error'
  | 'conflict'
  | 'request_timeout'
  | 'rate_limit_exceeded'
  | 'internal_server_error'
  | 'request_cancelled'
  | 'offline'
  | 'request_failed'

export class ApiClientError extends Error {
  public readonly status: number
  public readonly code: ApiErrorCode

  constructor(message: string, status: number, code: ApiErrorCode) {
    super(message)
    this.status = status
    this.code = code
    this.name = 'ApiClientError'
  }
}

const fallbackApiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5181').replace(
  /\/$/,
  '',
)
let runtimeConfigPromise: Promise<RuntimeConfig> | null = null

async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  if (import.meta.env.MODE === 'test') {
    return {}
  }

  runtimeConfigPromise ??= fetch('/config.json', { cache: 'no-store' })
    .then(async (response) => {
      if (!response.ok) {
        return {}
      }

      return (await response.json()) as RuntimeConfig
    })
    .catch(() => ({}))

  return runtimeConfigPromise
}

function buildJsonHeaders(init?: RequestInit, extraHeaders?: HeadersInit): Headers {
  const headers = new Headers(init?.headers)
  headers.set('Content-Type', 'application/json')

  if (extraHeaders) {
    new Headers(extraHeaders).forEach((value, key) => headers.set(key, value))
  }

  return headers
}

async function resolveApiBaseUrl(): Promise<string> {
  const config = await loadRuntimeConfig()
  return (config.apiBaseUrl ?? fallbackApiBaseUrl).replace(/\/$/, '')
}

function buildErrorMessage(
  status: number,
  body: LegacyErrorBody | ProblemDetailsBody | null,
): string {
  if (!body) {
    return `Request failed with status ${status}`
  }

  if ('error' in body && typeof body.error === 'string' && body.error.length > 0) {
    return body.error
  }

  if ('detail' in body && typeof body.detail === 'string' && body.detail.length > 0) {
    return body.detail
  }

  if ('errors' in body && body.errors) {
    const firstError = Object.values(body.errors).flat()[0]
    if (firstError) {
      return firstError
    }
  }

  if ('title' in body && typeof body.title === 'string' && body.title.length > 0) {
    return body.title
  }

  return `Request failed with status ${status}`
}

function resolveErrorCode(
  status: number,
  body: LegacyErrorBody | ProblemDetailsBody | null,
): ApiErrorCode {
  if (body && 'extensions' in body && body.extensions?.code) {
    return body.extensions.code as ApiErrorCode
  }

  if (body && 'code' in body && typeof body.code === 'string') {
    return body.code as ApiErrorCode
  }

  if (status === 400) return 'validation_error'
  if (status === 409) return 'conflict'
  if (status === 408) return 'request_timeout'
  if (status === 429) return 'rate_limit_exceeded'
  if (status >= 500) return 'internal_server_error'
  return 'request_failed'
}

async function parseError(response: Response): Promise<ApiClientError> {
  try {
    const errorBody = (await response.json()) as LegacyErrorBody | ProblemDetailsBody
    return new ApiClientError(
      buildErrorMessage(response.status, errorBody),
      response.status,
      resolveErrorCode(response.status, errorBody),
    )
  } catch {
    let plainErrorBody = ''
    try {
      plainErrorBody = (await response.text()).trim()
    } catch {
      // Ignore secondary parsing failures and return status-only message.
    }

    const plainErrorPreview =
      plainErrorBody.length > 160 ? `${plainErrorBody.slice(0, 160)}...` : plainErrorBody

    const message =
      plainErrorPreview.length > 0
        ? `Request failed with status ${response.status}: ${plainErrorPreview}`
        : `Request failed with status ${response.status}`

    return new ApiClientError(message, response.status, resolveErrorCode(response.status, null))
  }
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const apiBaseUrl = await resolveApiBaseUrl()
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: buildJsonHeaders(init),
      ...init,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiClientError('Request cancelled.', 0, 'request_cancelled')
    }

    throw new ApiClientError('Failed to fetch', 0, 'offline')
  }

  if (!response.ok) {
    throw await parseError(response)
  }

  return (await response.json()) as TResponse
}

function parseSseEvent(rawEvent: string): { eventName: string; payloadJson: string } | null {
  if (!rawEvent.trim()) {
    return null
  }

  let eventName = 'message'
  const dataLines: string[] = []

  for (const line of rawEvent.split('\n')) {
    if (line.startsWith('event:')) {
      eventName = line.slice('event:'.length).trim()
    }

    if (line.startsWith('data:')) {
      dataLines.push(line.slice('data:'.length).trim())
    }
  }

  if (dataLines.length === 0) {
    return null
  }

  return {
    eventName,
    payloadJson: dataLines.join('\n'),
  }
}

function handleSseEvent<TResponse>(
  rawEvent: string,
  handlers: { onDelta?: (delta: string) => void },
): TResponse | null {
  const parsedEvent = parseSseEvent(rawEvent)
  if (!parsedEvent) {
    return null
  }

  const payload = JSON.parse(parsedEvent.payloadJson) as TResponse | ChatStreamDeltaEvent

  if (
    parsedEvent.eventName === 'delta' &&
    typeof (payload as ChatStreamDeltaEvent).delta === 'string'
  ) {
    handlers.onDelta?.((payload as ChatStreamDeltaEvent).delta ?? '')
    return null
  }

  if (parsedEvent.eventName === 'complete') {
    return payload as TResponse
  }

  return null
}

async function consumeSseResponse<TResponse>(
  response: Response,
  handlers: { onDelta?: (delta: string) => void },
): Promise<TResponse> {
  const reader = response.body?.getReader()
  if (!reader) {
    throw new ApiClientError('Chat stream is unavailable.', 0, 'request_failed')
  }

  const decoder = new TextDecoder()
  let buffer = ''
  let finalResponse: TResponse | null = null

  while (true) {
    const { done, value } = await Promise.race([
      reader.read(),
      new Promise<ReadableStreamReadResult<Uint8Array>>((_, reject) => {
        setTimeout(
          () => reject(new ApiClientError('Chat stream timed out.', 408, 'request_timeout')),
          STREAM_READ_TIMEOUT_MS,
        )
      }),
    ])
    buffer += decoder.decode(value ?? new Uint8Array(), { stream: !done })

    const events = buffer.split('\n\n')
    buffer = events.pop() ?? ''

    for (const event of events) {
      finalResponse ??= handleSseEvent<TResponse>(event, handlers)
    }

    if (finalResponse) {
      await reader.cancel()
      return finalResponse
    }

    if (done) {
      break
    }
  }

  if (finalResponse) {
    return finalResponse
  }

  throw new ApiClientError('Chat stream ended without a completion payload.', 0, 'request_failed')
}

async function requestStream<TResponse>(
  path: string,
  init: RequestInit,
  handlers: { onDelta?: (delta: string) => void },
): Promise<TResponse> {
  const apiBaseUrl = await resolveApiBaseUrl()
  let response: Response

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: buildJsonHeaders(init, { Accept: 'text/event-stream' }),
      ...init,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiClientError('Request cancelled.', 0, 'request_cancelled')
    }

    throw new ApiClientError('Failed to fetch', 0, 'offline')
  }

  if (!response.ok) {
    throw await parseError(response)
  }

  if (!response.body || !response.headers.get('Content-Type')?.includes('text/event-stream')) {
    return (await response.json()) as TResponse
  }

  return consumeSseResponse<TResponse>(response, handlers)
}

// Separate helper for multipart/form-data — lets the browser set Content-Type with boundary.
async function requestFormData<TResponse>(
  path: string,
  body: FormData,
  signal?: AbortSignal,
): Promise<TResponse> {
  const apiBaseUrl = await resolveApiBaseUrl()
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: 'POST',
      body,
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiClientError('Request cancelled.', 0, 'request_cancelled')
    }

    throw new ApiClientError('Failed to fetch', 0, 'offline')
  }

  if (!response.ok) {
    throw await parseError(response)
  }

  return (await response.json()) as TResponse
}

export const apiClient = {
  getHealth(signal?: AbortSignal): Promise<HealthResponse> {
    return request<HealthResponse>('/api/v1/health', { signal })
  },
  ingest(payload: IngestRequest, signal?: AbortSignal): Promise<IngestResponse> {
    return request<IngestResponse>('/api/v1/ingest', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
    })
  },
  ingestFile(file: File, signal?: AbortSignal): Promise<IngestResponse> {
    const formData = new FormData()
    formData.append('file', file)
    return requestFormData<IngestResponse>('/api/v1/ingest/upload', formData, signal)
  },
  getIngestJobStatus(jobId: string, signal?: AbortSignal): Promise<IngestJobStatusResponse> {
    return request<IngestJobStatusResponse>(`/api/v1/ingest/jobs/${jobId}`, { signal })
  },
  getSourceDocument(
    source: string,
    knowledgeBaseId?: string,
    signal?: AbortSignal,
  ): Promise<SourceDocumentResponse> {
    const searchParams = new URLSearchParams({ source })
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    return request<SourceDocumentResponse>(`/api/v1/sources/document?${searchParams.toString()}`, {
      signal,
    })
  },
  getSourceUpdateAlert(
    knowledgeBaseId?: string,
    signal?: AbortSignal,
  ): Promise<SourceUpdateAlertResponse> {
    const searchParams = new URLSearchParams()
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    const suffix = searchParams.toString()
    const path = suffix ? `/api/v1/sources/update-alert?${suffix}` : '/api/v1/sources/update-alert'
    return request<SourceUpdateAlertResponse>(path, { signal })
  },
  listSources(knowledgeBaseId?: string, signal?: AbortSignal): Promise<SourceListItem[]> {
    const searchParams = new URLSearchParams()
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    const suffix = searchParams.toString()
    const path = suffix ? `/api/v1/sources?${suffix}` : '/api/v1/sources'
    return request<SourceListItem[]>(path, { signal })
  },
  deleteSource(
    source: string,
    knowledgeBaseId?: string,
    signal?: AbortSignal,
  ): Promise<SourceDeleteResponse> {
    const searchParams = new URLSearchParams({ source })
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    return request<SourceDeleteResponse>(`/api/v1/sources?${searchParams.toString()}`, {
      method: 'DELETE',
      signal,
    })
  },
  getSourceComparison(
    source: string,
    knowledgeBaseId?: string,
    citationChunkId?: string,
    signal?: AbortSignal,
  ): Promise<SourceComparisonResponse> {
    const searchParams = new URLSearchParams({ source })
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    if (citationChunkId) {
      searchParams.set('citationChunkId', citationChunkId)
    }

    return request<SourceComparisonResponse>(`/api/v1/sources/compare?${searchParams.toString()}`, {
      signal,
    })
  },
  getSourceQuality(
    source: string,
    knowledgeBaseId?: string,
    signal?: AbortSignal,
  ): Promise<SourceQualityReportResponse> {
    const searchParams = new URLSearchParams({ source })
    if (knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', knowledgeBaseId)
    }

    return request<SourceQualityReportResponse>(
      `/api/v1/sources/quality?${searchParams.toString()}`,
      {
        signal,
      },
    )
  },
  getOperatorAuditDashboard(
    options?: {
      knowledgeBaseId?: string
      feedbackType?: 'helpful' | 'unhelpful' | 'wrong-citation'
      lookbackHours?: number
    },
    signal?: AbortSignal,
  ): Promise<OperatorAuditDashboardResponse> {
    const searchParams = new URLSearchParams()
    if (options?.knowledgeBaseId) {
      searchParams.set('knowledgeBaseId', options.knowledgeBaseId)
    }

    if (options?.feedbackType) {
      searchParams.set('feedbackType', options.feedbackType)
    }

    if (typeof options?.lookbackHours === 'number') {
      searchParams.set('lookbackHours', String(options.lookbackHours))
    }

    const suffix = searchParams.toString()
    const path = suffix ? `/api/v1/operators/audit?${suffix}` : '/api/v1/operators/audit'
    return request<OperatorAuditDashboardResponse>(path, { signal })
  },
  getRetrievalBenchmarkDashboard(
    limit = 20,
    signal?: AbortSignal,
  ): Promise<RetrievalBenchmarkDashboardResponse> {
    return request<RetrievalBenchmarkDashboardResponse>(
      `/api/v1/operators/retrieval-benchmarks?limit=${limit}`,
      {
        signal,
      },
    )
  },
  runRetrievalBenchmark(signal?: AbortSignal): Promise<RetrievalBenchmarkEntry> {
    return request<RetrievalBenchmarkEntry>('/api/v1/operators/retrieval-benchmarks/run', {
      method: 'POST',
      signal,
    })
  },
  submitFeedback(
    payload: ConversationFeedbackRequest,
    signal?: AbortSignal,
  ): Promise<ConversationFeedbackResponse> {
    return request<ConversationFeedbackResponse>('/api/v1/chat/feedback', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
    })
  },
  chat(payload: ChatRequest, signal?: AbortSignal): Promise<ChatResponse> {
    return request<ChatResponse>('/api/v1/chat', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal,
    })
  },
  chatStream(
    payload: ChatRequest,
    handlers: { onDelta?: (delta: string) => void },
    signal?: AbortSignal,
  ): Promise<ChatResponse> {
    return requestStream<ChatResponse>(
      '/api/v1/chat/stream',
      {
        method: 'POST',
        body: JSON.stringify(payload),
        signal,
      },
      handlers,
    )
  },
}
