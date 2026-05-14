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
    let errorMessage = `Request failed with status ${response.status}`

    try {
      const errorBody = (await response.json()) as { error?: string }
      if (errorBody.error) {
        errorMessage = errorBody.error
      }
    } catch {
      errorMessage = `${errorMessage}. The backend may be offline or returned non-JSON content.`
    }

    throw new Error(errorMessage)
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
    let errorMessage = `Request failed with status ${response.status}`
    try {
      const errorBody = (await response.json()) as { error?: string }
      if (errorBody.error) errorMessage = errorBody.error
    } catch {
      // ignore JSON parse failure
    }
    throw new Error(errorMessage)
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
