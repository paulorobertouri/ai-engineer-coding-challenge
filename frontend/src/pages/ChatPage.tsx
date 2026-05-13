import { useCallback, useEffect, useState } from 'react'
import { apiClient } from '../services/apiClient'
import type { ChatMessage, Citation, IngestResponse, StatusMessage } from '../types/chat'
import { AppHeader } from '../components/AppHeader'
import { ChatComposer } from '../components/ChatComposer'
import { ChatTranscript } from '../components/ChatTranscript'
import { CitationsPanel } from '../components/CitationsPanel'
import { IngestPanel } from '../components/IngestPanel'
import { StatusBanner } from '../components/StatusBanner'

import { ChatRequestSchema, IngestRequestSchema, CHAT_MAX_MESSAGES } from '../types/validation'

function createMessage(role: ChatMessage['role'], content: string): ChatMessage {
  return {
    id: globalThis.crypto.randomUUID(),
    role,
    content,
    timestamp: new Date().toISOString(),
  }
}

export function ChatPage() {
  const [conversationId] = useState(() => globalThis.crypto.randomUUID())
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isIngesting, setIsIngesting] = useState(false)
  const [citations, setCitations] = useState<Citation[]>([])
  const [status, setStatus] = useState<StatusMessage>({
    tone: 'info',
    message: 'Checking backend health…',
  })
  const [messages, setMessages] = useState<ChatMessage[]>([])
  // null = health check in progress; false = not yet ingested; true = ingested
  const [isIngested, setIsIngested] = useState<boolean | null>(null)

  // Auto-dismiss success/info banners after 4 s
  useEffect(() => {
    if (status.tone === 'success' || status.tone === 'info') {
      const timer = setTimeout(
        () => setStatus((s) => (s === status ? { ...s, tone: 'info', message: '' } : s)),
        4000,
      )
      return () => clearTimeout(timer)
    }
  }, [status])

  useEffect(() => {
    let isCancelled = false

    async function loadHealth() {
      try {
        const health = await apiClient.getHealth()

        if (!isCancelled) {
          setIsIngested(health.isIngested)
          setStatus({
            tone: 'success',
            message: `${health.service} is running. ${health.notes[0] ?? ''}`.trim(),
          })
        }
      } catch (error) {
        if (!isCancelled) {
          if (error instanceof Error && error.message === 'Failed to fetch') {
            setStatus({
              tone: 'info',
              message: 'Backend health check failed: Failed to fetch. Retrying in 5s…',
            })
            setTimeout(() => {
              if (!isCancelled) void loadHealth()
            }, 5000)
          } else {
            setStatus({
              tone: 'warning',
              message:
                error instanceof Error
                  ? `Backend health check failed: ${error.message}`
                  : 'Backend health check failed.',
            })
          }
        }
      }
    }

    void loadHealth()

    return () => {
      isCancelled = true
    }
  }, [])

  const handleIngest = useCallback(async (file?: File) => {
    setIsIngesting(true)
    setStatus({
      tone: 'info',
      message: file ? `Uploading "${file.name}"…` : 'Calling the ingest endpoint…',
    })

    try {
      let response: IngestResponse
      if (file) {
        response = await apiClient.ingestFile(file)
      } else {
        const payload = IngestRequestSchema.parse({ forceReingest: false })
        response = await apiClient.ingest(payload)
      }

      setIsIngested(true)
      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `${response.message} Vector store: ${response.vectorStorePath}`,
      })
    } catch (error) {
      if (error instanceof Error && error.message.includes('already been ingested')) {
        setIsIngested(true)
        setStatus({ tone: 'success', message: 'Knowledge base is already ingested.' })
      } else {
        setStatus({
          tone: 'error',
          message: error instanceof Error ? error.message : 'Ingest request failed.',
        })
      }
    } finally {
      setIsIngesting(false)
    }
  }, [])

  const handleSend = useCallback(async () => {
    const trimmedDraft = draft.trim()
    if (!trimmedDraft) {
      return
    }

    const userMessage = createMessage('user', trimmedDraft)
    const nextMessages = [...messages, userMessage]

    setMessages(nextMessages)
    setDraft('')
    setIsSending(true)
    setStatus({ tone: 'info', message: 'Sending chat request…' })

    try {
      // Limit history sent to the API to avoid unbounded token growth
      const historyWindow = nextMessages.slice(-CHAT_MAX_MESSAGES)

      const payload = ChatRequestSchema.parse({
        conversationId,
        messages: historyWindow.map((message) => ({
          role: message.role,
          content: message.content,
          timestampUtc: message.timestamp,
        })),
      })

      const response = await apiClient.chat(payload)

      setMessages((currentMessages) => [
        ...currentMessages,
        createMessage('assistant', response.assistantMessage),
      ])
      setCitations(response.citations)
      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `Chat response received with status '${response.status}'.`,
      })
    } catch (error) {
      setMessages((currentMessages) => [
        ...currentMessages,
        createMessage('assistant', 'The chat request failed. Start the backend and try again.'),
      ])
      setStatus({
        tone: 'error',
        message: error instanceof Error ? error.message : 'Chat request failed.',
      })
    } finally {
      setIsSending(false)
    }
  }, [conversationId, draft, messages])

  const handlePromptSelect = useCallback((prompt: string) => {
    setDraft(prompt)
  }, [])

  const handleDismissStatus = useCallback(() => {
    setStatus({ tone: 'info', message: '' })
  }, [])

  if (!isIngested) {
    return (
      <main className="app-shell app-shell--setup">
        <div className="setup-layout">
          <AppHeader />
          {status.message && <StatusBanner status={status} onDismiss={handleDismissStatus} />}
          <IngestPanel onIngest={handleIngest} isBusy={isIngesting || isIngested === null} />
        </div>
      </main>
    )
  }

  return (
    <main className="app-shell">
      <section className="chat-layout">
        <AppHeader />
        {status.message && <StatusBanner status={status} onDismiss={handleDismissStatus} />}
        <ChatTranscript
          messages={messages}
          isSending={isSending}
          onPromptSelect={handlePromptSelect}
        />
        <ChatComposer value={draft} onChange={setDraft} onSubmit={handleSend} isBusy={isSending} />
      </section>

      <aside className="sidebar">
        <CitationsPanel citations={citations} hasMessages={messages.length > 0} />
      </aside>
    </main>
  )
}
