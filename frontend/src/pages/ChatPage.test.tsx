import { render, screen, waitFor, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ChatPage } from './ChatPage'
import { apiClient } from '../services/apiClient'

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getHealth: vi.fn(),
    chat: vi.fn(),
    chatStream: vi.fn(),
    ingest: vi.fn(),
    ingestFile: vi.fn(),
    listSources: vi.fn(),
    deleteSource: vi.fn(),
    submitFeedback: vi.fn(),
    getSourceDocument: vi.fn(),
    getSourceUpdateAlert: vi.fn(),
    getSourceComparison: vi.fn(),
    getSourceQuality: vi.fn(),
    getOperatorAuditDashboard: vi.fn(),
    getRetrievalBenchmarkDashboard: vi.fn(),
    runRetrievalBenchmark: vi.fn(),
  },
}))

// Health response when already ingested (shows chat layout)
const healthIngested = {
  status: 'ok',
  service: 'SOP API',
  utcTime: '2026-01-01T00:00:00Z',
  notes: ['All systems operational'],
  isIngested: true,
  recordCount: 10,
}

// Health response when NOT yet ingested (shows ingest panel)
const healthNotIngested = {
  ...healthIngested,
  isIngested: false,
  recordCount: 0,
}

const chatOk = {
  conversationId: 'conv-1',
  assistantMessage: 'Hello! How can I help?',
  status: 'ok',
  isPlaceholder: false,
  toolCalls: [],
  citations: [],
}

describe('ChatPage', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthIngested)
    vi.mocked(apiClient.chatStream).mockResolvedValue(chatOk)
    vi.mocked(apiClient.submitFeedback).mockResolvedValue({
      accepted: true,
      message: 'Feedback submitted successfully.',
      submittedAtUtc: '2026-05-15T16:00:00Z',
    })
    vi.mocked(apiClient.getSourceDocument).mockResolvedValue({
      source: 'Grocery_Store_SOP.md',
      knowledgeBaseId: 'default',
      chunks: [
        {
          chunkId: 'chunk-1',
          sectionTitle: 'Store Opening',
          content: 'Open the store at 7am.',
          startLine: 10,
          endLine: 12,
          index: 1,
        },
      ],
    })
    vi.mocked(apiClient.getSourceUpdateAlert).mockResolvedValue({
      knowledgeBaseId: 'default',
      requiresReingestReview: false,
      detectedAtUtc: '2026-05-15T16:00:00Z',
      message: 'No source update alert detected.',
    })
    vi.mocked(apiClient.listSources).mockResolvedValue([])
    vi.mocked(apiClient.deleteSource).mockResolvedValue({
      source: 'Grocery_Store_SOP.md',
      knowledgeBaseId: 'default',
      removedChunks: 1,
      message: "Removed 1 chunk(s) for source 'Grocery_Store_SOP.md'.",
    })
    vi.mocked(apiClient.getSourceComparison).mockResolvedValue({
      source: 'Grocery_Store_SOP.md',
      knowledgeBaseId: 'default',
      ingestedDocumentVersion: 'sha256:old123',
      currentDocumentVersion: 'sha256:new123',
      changedChunkCount: 1,
      totalComparedChunks: 1,
      chunks: [
        {
          index: 1,
          sectionTitle: 'Store Opening',
          changeType: 'modified',
          isImpactedCitation: true,
          ingestedContent: 'Open at 6:30am.',
          currentContent: 'Open at 7:00am.',
        },
      ],
    })
    vi.mocked(apiClient.getSourceQuality).mockResolvedValue({
      source: 'Grocery_Store_SOP.md',
      knowledgeBaseId: 'default',
      totalChunks: 1,
      duplicateSectionCount: 0,
      weakExtractionZoneCount: 0,
      shortestChunks: [],
      longestChunks: [],
    })
    vi.mocked(apiClient.getOperatorAuditDashboard).mockResolvedValue({
      generatedAtUtc: '2026-05-15T16:00:00Z',
      fromUtc: '2026-05-14T16:00:00Z',
      toUtc: '2026-05-15T16:00:00Z',
      feedbackCount: 0,
      lowConfidenceSignalCount: 0,
      failedIngestCount: 0,
      feedback: [],
      lowConfidenceSignals: [],
      failedIngests: [],
    })
    vi.mocked(apiClient.getRetrievalBenchmarkDashboard).mockResolvedValue({
      generatedAtUtc: '2026-05-15T16:00:00Z',
      entries: [
        {
          runId: 'run-1',
          timestampUtc: '2026-05-15T16:00:00Z',
          commit: 'local',
          fixtureCount: 3,
          precision: 0.8,
          recall: 0.7,
        },
      ],
    })
    vi.mocked(apiClient.runRetrievalBenchmark).mockResolvedValue({
      runId: 'run-2',
      timestampUtc: '2026-05-15T16:01:00Z',
      commit: 'local',
      fixtureCount: 3,
      precision: 0.82,
      recall: 0.72,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the page header', async () => {
    render(<ChatPage />)
    expect(screen.getByRole('heading', { name: 'SOP Assistant' })).toBeInTheDocument()
    expect(
      screen.getByText('Grocery Store Operating Procedures · Powered by AI'),
    ).toBeInTheDocument()
    expect(screen.getByText('GPT-5.4-mini')).toBeInTheDocument()

    await waitFor(() => {
      expect(apiClient.getHealth).toHaveBeenCalled()
    })
  })

  it('loads operator audit dashboard after health check', async () => {
    render(<ChatPage />)

    await waitFor(() => {
      expect(apiClient.getOperatorAuditDashboard).toHaveBeenCalledWith(
        expect.objectContaining({ lookbackHours: 168 }),
      )
    })
    expect(screen.getByRole('heading', { name: /operator audit/i })).toBeInTheDocument()
  })

  it('loads retrieval benchmark dashboard after health check', async () => {
    render(<ChatPage />)

    await waitFor(() => {
      expect(apiClient.getRetrievalBenchmarkDashboard).toHaveBeenCalledWith(20)
    })
    expect(screen.getByRole('heading', { name: /retrieval benchmarks/i })).toBeInTheDocument()
  })

  it('shows initial checking health status before the request completes', async () => {
    let resolveHealth!: (value: typeof healthIngested) => void
    vi.mocked(apiClient.getHealth).mockReturnValue(
      new Promise((resolve) => {
        resolveHealth = resolve
      }),
    )
    render(<ChatPage />)
    expect(screen.getByText('Checking backend health…')).toBeInTheDocument()

    await act(async () => {
      resolveHealth(healthIngested)
    })
  })

  it('shows success status after health check succeeds', async () => {
    render(<ChatPage />)
    await waitFor(() => {
      expect(screen.getByText('SOP API is running. All systems operational')).toBeInTheDocument()
    })
  })

  it('shows warning status when health check fails with a server error', async () => {
    vi.mocked(apiClient.getHealth).mockRejectedValueOnce(new Error('Internal Server Error'))
    render(<ChatPage />)
    await waitFor(() => {
      expect(
        screen.getByText(/Backend health check failed: Internal Server Error/i),
      ).toBeInTheDocument()
    })
  })

  it('keeps health success status when update alert request fails', async () => {
    vi.mocked(apiClient.getSourceUpdateAlert).mockRejectedValueOnce(
      new Error('Request failed with status 401'),
    )

    render(<ChatPage />)

    await waitFor(() => {
      expect(screen.getByText('SOP API is running. All systems operational')).toBeInTheDocument()
    })

    expect(screen.queryByText(/Backend health check failed/i)).not.toBeInTheDocument()
  })

  it('shows retry info message when health check fails with Failed to fetch', async () => {
    vi.mocked(apiClient.getHealth).mockRejectedValueOnce(new Error('Failed to fetch'))
    render(<ChatPage />)
    await waitFor(() => {
      expect(screen.getByText(/Retrying in 5s/i)).toBeInTheDocument()
    })
  })

  it('shows the empty transcript state when ingested', async () => {
    render(<ChatPage />)
    await waitFor(() => {
      expect(screen.getByText(/Ask anything about the operating procedures/i)).toBeInTheDocument()
    })
  })

  it('shows the ingest panel when not yet ingested', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    render(<ChatPage />)
    await waitFor(() => {
      expect(screen.getByText(/Knowledge Base/i)).toBeInTheDocument()
    })
  })

  it('appends the user message to the transcript after sending', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What are the opening steps?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('What are the opening steps?')).toBeInTheDocument()
    })
  })

  it('appends the assistant response to the transcript after sending', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What are the opening steps?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Hello! How can I help?')).toBeInTheDocument()
    })
  })

  it('sends selected response language in chat payload', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/answer language/i), {
      target: { value: 'es' },
    })
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'Que dice el SOP sobre reembolsos?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(apiClient.chatStream).toHaveBeenCalledWith(
        expect.objectContaining({ responseLanguage: 'es' }),
        expect.any(Object),
        expect.any(AbortSignal),
      )
    })
  })

  it('sends selected tone, length, and format in chat payload', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/answer tone/i), {
      target: { value: 'formal' },
    })
    fireEvent.change(screen.getByLabelText(/answer length/i), {
      target: { value: 'long' },
    })
    fireEvent.change(screen.getByLabelText(/answer format/i), {
      target: { value: 'bullets' },
    })
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'Provide opening policy details.' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(apiClient.chatStream).toHaveBeenCalledWith(
        expect.objectContaining({
          responseTone: 'formal',
          responseLength: 'long',
          responseFormat: 'bullets',
        }),
        expect.any(Object),
        expect.any(AbortSignal),
      )
    })
  })

  it('submits helpful feedback for an assistant message', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What are the opening steps?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Hello! How can I help?')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /^helpful$/i }))

    await waitFor(() => {
      expect(apiClient.submitFeedback).toHaveBeenCalledWith(
        expect.objectContaining({ feedbackType: 'helpful' }),
      )
    })
  })

  it('clears the draft after sending', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    const textarea = screen.getByLabelText(/ask about the grocery store sop/i)
    fireEvent.change(textarea, { target: { value: 'My question' } })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(textarea).toHaveValue('')
    })
  })

  it('shows "Sending chat request…" status while the request is in flight', async () => {
    let resolveChatResponse!: (value: typeof chatOk) => void
    vi.mocked(apiClient.chatStream).mockReturnValueOnce(
      new Promise((resolve) => {
        resolveChatResponse = resolve
      }),
    )
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'Hello' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    expect(screen.getByText('Sending chat request…')).toBeInTheDocument()

    resolveChatResponse(chatOk)
    await waitFor(() => {
      expect(screen.getByText(/Chat response received/i)).toBeInTheDocument()
    })
  })

  it('shows a chat error status when the chat request fails', async () => {
    vi.mocked(apiClient.chatStream).mockRejectedValueOnce(new Error('Network error'))
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'test message' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument()
    })
    expect(screen.getByRole('alert')).toHaveFocus()
  })

  it('adds a fallback assistant message in the transcript when the chat request fails', async () => {
    vi.mocked(apiClient.chatStream).mockRejectedValueOnce(new Error('Network error'))
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'test message' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText(/The chat request failed/i)).toBeInTheDocument()
    })
  })

  it('shows citations in the panel after a chat response with citations', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce({
      ...chatOk,
      citations: [
        {
          source: 'Grocery_Store_SOP.md',
          snippet: 'Open the store at 7am.',
          startLine: 10,
          endLine: 12,
        },
      ],
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What time do we open?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Open the store at 7am.')).toBeInTheDocument()
    })
  })

  it('renders dynamic follow-up suggestions from structured output and lets users select one', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce({
      ...chatOk,
      structuredOutput: {
        answerText: chatOk.assistantMessage,
        citedChunkIds: ['chunk-1'],
        followUpSuggestions: [
          'Can you summarize the cited opening checklist points?',
          'What common opening mistakes should I avoid?',
        ],
      },
    })

    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What are opening steps?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText(/Suggested follow-up questions/i)).toBeInTheDocument()
      expect(
        screen.getByText(/Can you summarize the cited opening checklist points/i),
      ).toBeInTheDocument()
    })

    fireEvent.click(
      screen.getByRole('button', { name: /What common opening mistakes should I avoid/i }),
    )

    await waitFor(() => {
      expect(screen.getByLabelText(/ask about the grocery store sop/i)).toHaveValue(
        'What common opening mistakes should I avoid?',
      )
    })
  })

  it('loads source viewer content when a citation is selected', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce({
      ...chatOk,
      citations: [
        {
          source: 'Grocery_Store_SOP.md',
          chunkId: 'chunk-1',
          snippet: 'Open the store at 7am.',
          startLine: 10,
          endLine: 12,
          knowledgeBaseId: 'default',
        },
      ],
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'What time do we open?' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Open the store at 7am.')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByText('Open the store at 7am.'))

    await waitFor(() => {
      expect(apiClient.getSourceDocument).toHaveBeenCalledWith(
        'Grocery_Store_SOP.md',
        'default',
        expect.any(AbortSignal),
      )
      expect(apiClient.getSourceComparison).toHaveBeenCalledWith(
        'Grocery_Store_SOP.md',
        'default',
        'chunk-1',
        expect.any(AbortSignal),
      )
      expect(apiClient.getSourceQuality).toHaveBeenCalledWith(
        'Grocery_Store_SOP.md',
        'default',
        expect.any(AbortSignal),
      )
      expect(screen.getByRole('heading', { name: /source viewer/i })).toBeInTheDocument()
      expect(screen.getAllByText(/Store Opening/).length).toBeGreaterThan(0)
    })
  })

  it('updates status to success after ingest completes', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    vi.mocked(apiClient.ingest).mockResolvedValueOnce({
      accepted: true,
      message: 'Ingestion complete.',
      sourcePath: 'knowledge-base/Grocery_Store_SOP.md',
      chunksCreated: 10,
      recordsPersisted: 10,
      vectorStorePath: '/store',
      isPlaceholder: false,
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByRole('button', { name: /use default sop/i }))
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))

    await waitFor(() => {
      expect(screen.getByText(/Ingestion complete/i)).toBeInTheDocument()
    })
  })

  it('shows error status when ingest fails', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    vi.mocked(apiClient.ingest).mockRejectedValueOnce(new Error('Ingest failed'))
    render(<ChatPage />)

    await waitFor(() => screen.getByRole('button', { name: /use default sop/i }))
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))

    await waitFor(() => {
      expect(screen.getByText('Ingest failed')).toBeInTheDocument()
    })
  })

  it('shows "Calling the ingest endpoint…" status while ingesting', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    let resolveIngest!: (
      value: ReturnType<(typeof apiClient)['ingest']> extends Promise<infer T> ? T : never,
    ) => void
    vi.mocked(apiClient.ingest).mockReturnValueOnce(
      new Promise((resolve) => {
        resolveIngest = resolve
      }),
    )
    render(<ChatPage />)

    await waitFor(() => screen.getByRole('button', { name: /use default sop/i }))
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))

    expect(screen.getByText('Calling the ingest endpoint…')).toBeInTheDocument()

    resolveIngest({
      accepted: true,
      message: 'Done.',
      sourcePath: '',
      chunksCreated: 0,
      recordsPersisted: 0,
      vectorStorePath: '',
      isPlaceholder: false,
    })
    await waitFor(() => {
      expect(screen.getByText(/Done\. Vector store:/i)).toBeInTheDocument()
    })
  })

  it('uploads a file and transitions to the chat layout on success', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    vi.mocked(apiClient.ingestFile).mockResolvedValueOnce({
      accepted: true,
      message: 'File ingested.',
      sourcePath: 'report.md',
      chunksCreated: 5,
      recordsPersisted: 5,
      vectorStorePath: '/store',
      isPlaceholder: false,
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/select a .md or .txt document/i))
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    const file = new File(['# Report'], 'report.md')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: /upload & ingest/i }))

    await waitFor(() => {
      expect(screen.getByText(/File ingested/i)).toBeInTheDocument()
    })
    expect(apiClient.ingestFile).toHaveBeenCalledWith(file, expect.any(AbortSignal))
  })

  it('shows uploading status message while uploading a file', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    let resolveUpload!: (value: Awaited<ReturnType<typeof apiClient.ingestFile>>) => void
    vi.mocked(apiClient.ingestFile).mockReturnValueOnce(
      new Promise((r) => {
        resolveUpload = r
      }),
    )
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/select a .md or .txt document/i))
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    fireEvent.change(input, { target: { files: [new File(['x'], 'my-notes.md')] } })
    fireEvent.click(screen.getByRole('button', { name: /upload & ingest/i }))

    expect(screen.getByText(/Uploading "my-notes.md"/i)).toBeInTheDocument()

    resolveUpload({
      accepted: true,
      message: 'Done.',
      sourcePath: '',
      chunksCreated: 0,
      recordsPersisted: 0,
      vectorStorePath: '',
      isPlaceholder: false,
    })
    await waitFor(() => {
      expect(screen.getByText(/Done\. Vector store:/i)).toBeInTheDocument()
    })
  })

  it('shows warning tone when ingest response is a placeholder', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    vi.mocked(apiClient.ingest).mockResolvedValueOnce({
      accepted: true,
      message: 'Placeholder ingested.',
      sourcePath: '',
      chunksCreated: 0,
      recordsPersisted: 0,
      vectorStorePath: '',
      isPlaceholder: true,
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByRole('button', { name: /use default sop/i }))
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))

    await waitFor(() => {
      expect(document.querySelector('.status-banner')).toHaveAttribute('data-tone', 'warning')
    })
  })

  it('handles "already been ingested" ingest error by marking as ingested', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthNotIngested)
    vi.mocked(apiClient.ingest).mockRejectedValueOnce(new Error('has already been ingested'))
    render(<ChatPage />)

    await waitFor(() => screen.getByRole('button', { name: /use default sop/i }))
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))

    await waitFor(() => {
      expect(screen.getByText(/already ingested/i)).toBeInTheDocument()
    })
  })

  it('dismisses the status banner when the dismiss button is clicked', async () => {
    render(<ChatPage />)

    await waitFor(() => screen.getByText('SOP API is running. All systems operational'))
    fireEvent.click(screen.getByRole('button', { name: /dismiss/i }))

    expect(
      screen.queryByText('SOP API is running. All systems operational'),
    ).not.toBeInTheDocument()
  })

  it('fills the draft when a suggested prompt chip is clicked', async () => {
    render(<ChatPage />)

    await waitFor(() =>
      screen.getAllByRole('button', {
        name: /opening procedures|operating hours|complaint|closing checklist/i,
      }),
    )
    const chip = screen.getAllByRole('button', {
      name: /opening procedures|operating hours|complaint|closing checklist/i,
    })[0]
    fireEvent.click(chip)

    await waitFor(() => {
      const textarea = screen.getByLabelText(/ask about the grocery store sop/i)
      expect((textarea as HTMLTextAreaElement).value).not.toBe('')
    })
  })

  it('shows warning tone when chat response is a placeholder', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce({
      ...chatOk,
      isPlaceholder: true,
    })
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'Test question' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(document.querySelector('.status-banner')).toHaveAttribute('data-tone', 'warning')
    })
  })

  it('shows proactive SOP update alert when checksum drift is detected', async () => {
    vi.mocked(apiClient.getSourceUpdateAlert).mockResolvedValueOnce({
      knowledgeBaseId: 'default',
      requiresReingestReview: true,
      currentSourceChecksum: 'new-hash',
      ingestedSourceChecksum: 'old-hash',
      detectedAtUtc: '2026-05-15T16:00:00Z',
      message:
        'Source document checksum changed since the last ingest. Reingest/review is recommended.',
    })

    render(<ChatPage />)

    await waitFor(() => {
      expect(screen.getByText(/reingest\/review is recommended/i)).toBeInTheDocument()
      expect(document.querySelector('.status-banner')).toHaveAttribute('data-tone', 'warning')
    })
  })

  it('shows error message string from non-Error chat failure', async () => {
    vi.mocked(apiClient.chatStream).mockRejectedValueOnce('plain string error')
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'Hello' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Chat request failed.')).toBeInTheDocument()
    })
  })

  it('shows offline badge when backend reports fallback mode', async () => {
    vi.mocked(apiClient.getHealth).mockResolvedValueOnce({
      ...healthIngested,
      notes: ['Service is running in offline/fallback mode (no OpenAI API key).'],
    })

    render(<ChatPage />)

    await waitFor(() => {
      expect(screen.getByText('Offline Mode')).toBeInTheDocument()
    })
  })

  it('retries the last failed chat without retyping', async () => {
    vi.mocked(apiClient.chatStream)
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce(chatOk)

    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'retry me' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry last failed message/i })).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /retry last failed message/i }))

    await waitFor(() => {
      expect(vi.mocked(apiClient.chatStream).mock.calls.length).toBeGreaterThanOrEqual(2)
      expect(screen.getByText('Hello! How can I help?')).toBeInTheDocument()
    })
  })

  it('starts a fresh conversation when New chat is clicked', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'hello' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Hello! How can I help?')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /new chat/i }))

    await waitFor(() => {
      expect(screen.queryByText('Hello! How can I help?')).not.toBeInTheDocument()
      expect(screen.getByText(/Started a new conversation/i)).toBeInTheDocument()
    })
  })

  it('exports the conversation transcript as a markdown download', async () => {
    vi.mocked(apiClient.chatStream).mockResolvedValueOnce(chatOk)
    const createObjectUrlSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock')
    const revokeObjectUrlSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})

    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'hello export' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /export conversation/i })).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: /export conversation/i }))

    await waitFor(() => {
      expect(createObjectUrlSpy).toHaveBeenCalled()
      expect(clickSpy).toHaveBeenCalled()
      expect(revokeObjectUrlSpy).toHaveBeenCalledWith('blob:mock')
    })
  })

  it('cancels an in-flight chat request when a new send starts', async () => {
    let resolveFirst!: (value: typeof chatOk) => void
    vi.mocked(apiClient.chatStream)
      .mockReturnValueOnce(
        new Promise((resolve) => {
          resolveFirst = resolve
        }),
      )
      .mockResolvedValueOnce(chatOk)

    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'first' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'second' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    resolveFirst(chatOk)

    await waitFor(() => {
      expect(vi.mocked(apiClient.chatStream).mock.calls.length).toBeGreaterThanOrEqual(2)
    })
  })

  it('opens keyboard shortcut map when pressing question mark', async () => {
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))
    fireEvent.keyDown(globalThis, { key: '?' })

    await waitFor(() => {
      expect(screen.getByRole('dialog', { name: /keyboard shortcut map/i })).toBeInTheDocument()
      expect(screen.getByText(/Alt \+ N/i)).toBeInTheDocument()
    })
  })
})
