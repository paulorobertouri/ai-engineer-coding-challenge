import type {
  ChatRequest,
  ChatResponse,
  HealthResponse,
  IngestRequest,
  IngestResponse,
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

type ApiErrorCode =
  | 'validation_error'
  | 'conflict'
  | 'rate_limit_exceeded'
  | 'internal_server_error'
  | 'offline'
  | 'request_failed'

export class ApiClientError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: ApiErrorCode,
  ) {
    super(message)
    this.name = 'ApiClientError'
  }
}

const fallbackApiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5181').replace(
  /\/$/, '',
)
let runtimeConfigPromise: Promise<RuntimeConfig> | null = null

async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  if (import.meta.env.MODE === 'test') {
    return {}
  }

  if (!runtimeConfigPromise) {
    runtimeConfigPromise = fetch('/config.json', { cache: 'no-store' })
      .then(async (response) => {
        if (!response.ok) {
          return {}
        }

        return (await response.json()) as RuntimeConfig
      })
      .catch(() => ({}))
  }

  return runtimeConfigPromise
}

async function resolveApiBaseUrl(): Promise<string> {
  const config = await loadRuntimeConfig()
  return (config.apiBaseUrl ?? fallbackApiBaseUrl).replace(/\/$/, '')
}

function buildErrorMessage(status: number, body: LegacyErrorBody | ProblemDetailsBody | null): string {
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
    return new ApiClientError(
      `Request failed with status ${response.status}. The backend may be offline or returned non-JSON content.`,
      response.status,
      'offline',
    )
  }
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  const apiBaseUrl = await resolveApiBaseUrl()
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (!response.ok) {
    throw await parseError(response)
  }

  return (await response.json()) as TResponse
}

// Separate helper for multipart/form-data — lets the browser set Content-Type with boundary.
async function requestFormData<TResponse>(path: string, body: FormData): Promise<TResponse> {
  const apiBaseUrl = await resolveApiBaseUrl()
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    body,
  })

  if (!response.ok) {
    throw await parseError(response)
  }

  return (await response.json()) as TResponse
}

export const apiClient = {
  getHealth(): Promise<HealthResponse> {
    return request<HealthResponse>('/api/v1/health')
  },
  ingest(payload: IngestRequest): Promise<IngestResponse> {
    return request<IngestResponse>('/api/v1/ingest', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  },
  ingestFile(file: File): Promise<IngestResponse> {
    const formData = new FormData()
    formData.append('file', file)
    return requestFormData<IngestResponse>('/api/v1/ingest/upload', formData)
  },
  chat(payload: ChatRequest): Promise<ChatResponse> {
    return request<ChatResponse>('/api/v1/chat', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  },
}
