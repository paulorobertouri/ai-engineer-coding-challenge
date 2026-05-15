import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { apiClient, ApiClientError } from './apiClient'

function mockFetch(data: unknown, ok = true, status = 200) {
  vi.mocked(fetch).mockResolvedValueOnce({
    ok,
    status,
    json: async () => data,
  } as Response)
}

function toUrlString(url: RequestInfo | URL): string {
  if (typeof url === 'string') {
    return url
  }

  if (url instanceof URL) {
    return url.toString()
  }

  return url.url
}

describe('apiClient', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('getHealth calls /api/health and returns health data', async () => {
    const health = { status: 'ok', service: 'test', utcTime: '', notes: [] }
    mockFetch(health)
    const result = await apiClient.getHealth()
    expect(result.status).toBe('ok')
    const [url, init] = vi.mocked(fetch).mock.calls[0]
    expect(toUrlString(url)).toContain('/api/v1/health')
    expect(new Headers(init?.headers).get('Content-Type')).toBe('application/json')
  })

  it('ingest sends POST to /api/ingest and returns response', async () => {
    const ingestResponse = {
      accepted: true,
      message: 'done',
      sourcePath: '/path',
      chunksCreated: 5,
      recordsPersisted: 5,
      vectorStorePath: '/store',
      isPlaceholder: false,
    }
    mockFetch(ingestResponse)
    const result = await apiClient.ingest({ sourcePath: '/path', forceReingest: false })
    expect(result.accepted).toBe(true)
    expect(result.chunksCreated).toBe(5)
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/ingest'),
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('chat sends POST to /api/chat and returns response', async () => {
    const chatResponse = {
      conversationId: 'c1',
      assistantMessage: 'Hi there',
      status: 'success',
      isPlaceholder: false,
      toolCalls: [],
      citations: [],
    }
    mockFetch(chatResponse)
    const result = await apiClient.chat({ conversationId: 'c1', messages: [] })
    expect(result.assistantMessage).toBe('Hi there')
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/chat'),
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('submitFeedback sends POST to /api/v1/chat/feedback and returns response', async () => {
    const feedbackResponse = {
      accepted: true,
      message: 'Feedback submitted successfully.',
      submittedAtUtc: '2026-05-15T16:00:00Z',
    }

    mockFetch(feedbackResponse)
    const result = await apiClient.submitFeedback({
      conversationId: 'conv-1',
      messageId: 'assistant-1',
      feedbackType: 'helpful',
    })

    expect(result.accepted).toBe(true)
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/chat/feedback'),
      expect.objectContaining({ method: 'POST' }),
    )
  })

  it('getSourceDocument sends GET to /api/v1/sources/document with query params', async () => {
    mockFetch({
      source: 'Grocery_Store_SOP.md',
      knowledgeBaseId: 'default',
      chunks: [],
    })

    const result = await apiClient.getSourceDocument('Grocery_Store_SOP.md', 'default')

    expect(result.source).toBe('Grocery_Store_SOP.md')
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining(
        '/api/v1/sources/document?source=Grocery_Store_SOP.md&knowledgeBaseId=default',
      ),
      expect.objectContaining({
        headers: expect.any(Headers),
      }),
    )
  })

  it('chatStream emits deltas and returns the final response', async () => {
    const streamBody = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(
          new TextEncoder().encode(
            'event: delta\ndata: {"delta":"Hello "}\n\nevent: delta\ndata: {"delta":"there"}\n\nevent: complete\ndata: {"conversationId":"c1","assistantMessage":"Hello there","status":"success","isPlaceholder":false,"toolCalls":[],"citations":[]}\n\n',
          ),
        )
        controller.close()
      },
    })

    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'text/event-stream' }),
      body: streamBody,
    } as Response)

    const deltas: string[] = []
    const result = await apiClient.chatStream(
      { conversationId: 'c1', messages: [] },
      { onDelta: (delta) => deltas.push(delta) },
    )

    expect(deltas).toEqual(['Hello ', 'there'])
    expect(result.assistantMessage).toBe('Hello there')
  })

  it('throws the error field from a non-ok response', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: async () => ({ error: 'Bad input' }),
    } as Response)
    await expect(apiClient.getHealth()).rejects.toThrow('Bad input')
  })

  it('throws a status message when non-ok response has no error field', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 503,
      json: async () => ({}),
    } as Response)
    await expect(apiClient.getHealth()).rejects.toThrow(/503/)
  })

  it('throws when response json is not parseable', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error('Invalid JSON')
      },
    } as unknown as Response)
    await expect(apiClient.getHealth()).rejects.toThrow(/500/)
  })

  it('parses ProblemDetails detail field from non-ok response', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => ({
        title: 'Knowledge base already ingested.',
        detail: 'The knowledge base has already been ingested. Re-ingestion is not permitted.',
        extensions: { code: 'conflict' },
      }),
    } as Response)

    await expect(apiClient.ingest({ forceReingest: false })).rejects.toThrow(
      /already been ingested/i,
    )
  })

  it('parses ValidationProblemDetails errors payload', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: async () => ({
        title: 'One or more validation errors occurred.',
        errors: {
          Messages: ['At least one chat message is required.'],
        },
        extensions: { code: 'validation_error' },
      }),
    } as Response)

    await expect(apiClient.chat({ conversationId: 'c1', messages: [] })).rejects.toThrow(
      /At least one chat message is required/i,
    )
  })

  it('returns typed ApiClientError for rate-limit problem details', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 429,
      json: async () => ({
        title: 'Rate limit exceeded.',
        detail: 'Too many requests were sent in a short period. Please retry later.',
        extensions: { code: 'rate_limit_exceeded' },
      }),
    } as Response)

    try {
      await apiClient.chat({ conversationId: 'c1', messages: [] })
      throw new Error('Expected rate-limit error')
    } catch (error) {
      expect(error).toBeInstanceOf(ApiClientError)
      const apiError = error as ApiClientError
      expect(apiError.code).toBe('rate_limit_exceeded')
      expect(apiError.status).toBe(429)
    }
  })

  it('returns request_cancelled for aborted fetches', async () => {
    vi.mocked(fetch).mockRejectedValueOnce(
      new DOMException('The operation was aborted.', 'AbortError'),
    )
    const controller = new AbortController()
    controller.abort()

    await expect(apiClient.getHealth(controller.signal)).rejects.toMatchObject({
      code: 'request_cancelled',
      status: 0,
    })
  })

  it('ingestFile sends a multipart POST to /api/v1/ingest/upload', async () => {
    const ingestResponse = {
      accepted: true,
      message: 'done',
      sourcePath: 'my-file.md',
      chunksCreated: 3,
      recordsPersisted: 3,
      vectorStorePath: '/store',
      isPlaceholder: false,
    }
    mockFetch(ingestResponse)
    const file = new File(['# Hello'], 'my-file.md', { type: 'text/markdown' })
    const result = await apiClient.ingestFile(file)
    expect(result.accepted).toBe(true)
    expect(result.chunksCreated).toBe(3)
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/ingest/upload'),
      expect.objectContaining({ method: 'POST', body: expect.any(FormData) }),
    )
  })

  it('ingestFile appends the file under the "file" key', async () => {
    mockFetch({
      accepted: true,
      message: '',
      sourcePath: '',
      chunksCreated: 0,
      recordsPersisted: 0,
      vectorStorePath: '',
      isPlaceholder: false,
    })
    const file = new File(['data'], 'doc.txt')
    await apiClient.ingestFile(file)
    const [, init] = vi.mocked(fetch).mock.calls[0]
    expect(init?.body).toBeInstanceOf(FormData)
    const formData = init?.body as FormData
    expect(formData.get('file')).toBe(file)
  })

  it('ingestFile throws the error field from a non-ok response', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => ({ error: 'Already ingested' }),
    } as Response)
    const file = new File(['data'], 'doc.md')
    await expect(apiClient.ingestFile(file)).rejects.toThrow('Already ingested')
  })

  it('ingestFile throws a status message when non-ok response has no error field', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 413,
      json: async () => ({}),
    } as Response)
    const file = new File(['data'], 'doc.md')
    await expect(apiClient.ingestFile(file)).rejects.toThrow(/413/)
  })

  it('ingestFile swallows JSON parse errors on non-ok responses and still throws', async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error('bad json')
      },
    } as unknown as Response)
    const file = new File(['data'], 'doc.md')
    await expect(apiClient.ingestFile(file)).rejects.toThrow(/500/)
  })
})
