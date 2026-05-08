import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ChatPage } from './ChatPage'
import { apiClient } from '../services/apiClient'

vi.mock('../services/apiClient', () => ({
  apiClient: {
    getHealth: vi.fn(),
    chat: vi.fn(),
    ingest: vi.fn(),
    ingestFile: vi.fn(),
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
    vi.mocked(apiClient.getHealth).mockResolvedValue(healthIngested)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders the page header', () => {
    render(<ChatPage />)
    expect(screen.getByRole('heading', { name: 'SOP Assistant' })).toBeInTheDocument()
    expect(
      screen.getByText('Grocery Store Operating Procedures · Powered by AI'),
    ).toBeInTheDocument()
    expect(screen.getByText('GPT-4o-mini')).toBeInTheDocument()
  })

  it('shows initial checking health status before the request completes', () => {
    vi.mocked(apiClient.getHealth).mockReturnValue(new Promise(() => {}))
    render(<ChatPage />)
    expect(screen.getByText('Checking backend health…')).toBeInTheDocument()
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
    vi.mocked(apiClient.chat).mockResolvedValueOnce(chatOk)
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
    vi.mocked(apiClient.chat).mockResolvedValueOnce(chatOk)
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

  it('clears the draft after sending', async () => {
    vi.mocked(apiClient.chat).mockResolvedValueOnce(chatOk)
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
    vi.mocked(apiClient.chat).mockReturnValueOnce(
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
    vi.mocked(apiClient.chat).mockRejectedValueOnce(new Error('Network error'))
    render(<ChatPage />)

    await waitFor(() => screen.getByLabelText(/ask about the grocery store sop/i))

    fireEvent.change(screen.getByLabelText(/ask about the grocery store sop/i), {
      target: { value: 'test message' },
    })
    fireEvent.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument()
    })
  })

  it('adds a fallback assistant message in the transcript when the chat request fails', async () => {
    vi.mocked(apiClient.chat).mockRejectedValueOnce(new Error('Network error'))
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
    vi.mocked(apiClient.chat).mockResolvedValueOnce({
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
    expect(apiClient.ingestFile).toHaveBeenCalledWith(file)
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
    vi.mocked(apiClient.chat).mockResolvedValueOnce({
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

  it('shows error message string from non-Error chat failure', async () => {
    vi.mocked(apiClient.chat).mockRejectedValueOnce('plain string error')
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
})
