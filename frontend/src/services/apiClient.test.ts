import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { apiClient } from './apiClient'

describe('apiClient', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function mockFetch(data: unknown, ok = true, status = 200) {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok,
      status,
      json: async () => data,
    } as Response)
  }

  it('getHealth calls /api/health and returns health data', async () => {
    const health = { status: 'ok', service: 'test', utcTime: '', notes: [] }
    mockFetch(health)
    const result = await apiClient.getHealth()
    expect(result.status).toBe('ok')
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/health'),
      expect.objectContaining({
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    )
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
    const result = await apiClient.chat({ conversationId: 'c1', messages: [], useTools: true })
    expect(result.assistantMessage).toBe('Hi there')
    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/chat'),
      expect.objectContaining({ method: 'POST' }),
    )
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
})
