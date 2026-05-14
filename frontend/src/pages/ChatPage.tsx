import { useCallback, useEffect, useRef, useState } from 'react'
import { apiClient } from '../services/apiClient'
import type { ChatMessage, Citation, IngestResponse, StatusMessage } from '../types/chat'
import { AppHeader } from '../components/AppHeader'
import { ChatComposer } from '../components/ChatComposer'
import { ChatTranscript } from '../components/ChatTranscript'
import { CitationsPanel } from '../components/CitationsPanel'
import { IngestPanel } from '../components/IngestPanel'
import { StatusBanner } from '../components/StatusBanner'

import { ChatRequestSchema, IngestRequestSchema, CHAT_MAX_MESSAGES } from '../types/validation'

const CHAT_SESSION_KEY = 'sop-assistant-chat-session-v1'

interface StoredChatSession {
  conversationId: string
  messages: ChatMessage[]
  citations: Citation[]
}

function createMessage(role: ChatMessage['role'], content: string): ChatMessage {
  return {
    id: globalThis.crypto.randomUUID(),
    role,
    content,
    timestamp: new Date().toISOString(),
  }
}

function isRequestCancelledError(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    'code' in error &&
    (error as { code?: string }).code === 'request_cancelled'
  )
}

export function ChatPage() {
  const [conversationId, setConversationId] = useState<string>(() => globalThis.crypto.randomUUID())
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isIngesting, setIsIngesting] = useState(false)
  const [isOfflineMode, setIsOfflineMode] = useState(false)
  const [lastFailedDraft, setLastFailedDraft] = useState<string | null>(null)
  const [hasIngestToRetry, setHasIngestToRetry] = useState(false)
  const [lastIngestFile, setLastIngestFile] = useState<File | undefined>(undefined)
  const [citations, setCitations] = useState<Citation[]>([])
  const [status, setStatus] = useState<StatusMessage>({
    tone: 'info',
    message: 'Checking backend health…',
  })
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const chatAbortRef = useRef<AbortController | null>(null)
  const ingestAbortRef = useRef<AbortController | null>(null)
  const statusBannerRef = useRef<HTMLElement | null>(null)
  // null = health check in progress; false = not yet ingested; true = ingested
  const [isIngested, setIsIngested] = useState<boolean | null>(null)

  useEffect(() => {
    try {
      const raw = sessionStorage.getItem(CHAT_SESSION_KEY)
      if (!raw) {
        return
      }

      const stored = JSON.parse(raw) as StoredChatSession
      if (stored.conversationId) {
        setConversationId(stored.conversationId)
      }
      if (Array.isArray(stored.messages)) {
        setMessages(stored.messages)
      }
      if (Array.isArray(stored.citations)) {
        setCitations(stored.citations)
      }
    } catch {
      sessionStorage.removeItem(CHAT_SESSION_KEY)
    }
  }, [])

  useEffect(() => {
    const snapshot: StoredChatSession = {
      conversationId,
      messages,
      citations,
    }
    sessionStorage.setItem(CHAT_SESSION_KEY, JSON.stringify(snapshot))
  }, [conversationId, messages, citations])

  useEffect(() => {
    return () => {
      chatAbortRef.current?.abort()
      ingestAbortRef.current?.abort()
    }
  }, [])

  // Auto-dismiss success/info banners after 4 s
  useEffect(() => {
    if (status.tone === 'error') {
      statusBannerRef.current?.focus()
    }
  }, [status.tone, status.message])

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
          const isFallbackMode = health.notes.some((note) => /fallback|no openai api key/i.test(note))
          setIsOfflineMode(isFallbackMode)
          setIsIngested(health.isIngested)
          setStatus({
            tone: isFallbackMode ? 'warning' : 'success',
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
    ingestAbortRef.current?.abort()
    const abortController = new AbortController()
    ingestAbortRef.current = abortController

    setIsIngesting(true)
    setHasIngestToRetry(true)
    setLastIngestFile(file)
    setStatus({
      tone: 'info',
      message: file ? `Uploading "${file.name}"…` : 'Calling the ingest endpoint…',
    })

    try {
      let response: IngestResponse
      if (file) {
        response = await apiClient.ingestFile(file, abortController.signal)
      } else {
        const payload = IngestRequestSchema.parse({ forceReingest: false })
        response = await apiClient.ingest(payload, abortController.signal)
      }

      setIsIngested(true)
      setHasIngestToRetry(false)
      setLastIngestFile(undefined)
      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `${response.message} Vector store: ${response.vectorStorePath}`,
      })
    } catch (error) {
      if (isRequestCancelledError(error)) {
        setStatus({ tone: 'info', message: 'Ingest request cancelled.' })
        return
      }

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
      if (ingestAbortRef.current === abortController) {
        ingestAbortRef.current = null
      }
    }
  }, [])

  const submitChat = useCallback(
    async (messageText: string, clearDraft: boolean) => {
      chatAbortRef.current?.abort()
      const abortController = new AbortController()
      chatAbortRef.current = abortController

      const userMessage = createMessage('user', messageText)
      const nextMessages = [...messages, userMessage]

      setMessages(nextMessages)
      if (clearDraft) {
        setDraft('')
      }
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

        const response = await apiClient.chat(payload, abortController.signal)

        setMessages((currentMessages) => [
          ...currentMessages,
          createMessage('assistant', response.assistantMessage),
        ])
        setLastFailedDraft(null)
        setCitations(response.citations)
        setStatus({
          tone: response.isPlaceholder ? 'warning' : 'success',
          message: `Chat response received with status '${response.status}'.`,
        })
      } catch (error) {
        if (isRequestCancelledError(error)) {
          setStatus({ tone: 'info', message: 'Chat request cancelled.' })
          return
        }

        setMessages((currentMessages) => [
          ...currentMessages,
          createMessage('assistant', 'The chat request failed. Start the backend and try again.'),
        ])
        setLastFailedDraft(messageText)
        setStatus({
          tone: 'error',
          message: error instanceof Error ? error.message : 'Chat request failed.',
        })
      } finally {
        setIsSending(false)
        if (chatAbortRef.current === abortController) {
          chatAbortRef.current = null
        }
      }
    },
    [conversationId, messages],
  )

  const handleSend = useCallback(async () => {
    const trimmedDraft = draft.trim()
    if (!trimmedDraft) {
      return
    }

    await submitChat(trimmedDraft, true)
  }, [draft, submitChat])

  const handlePromptSelect = useCallback((prompt: string) => {
    setDraft(prompt)
  }, [])

  const handleDismissStatus = useCallback(() => {
    setStatus({ tone: 'info', message: '' })
  }, [])

  const handleRetryChat = useCallback(async () => {
    if (!lastFailedDraft || isSending) {
      return
    }

    await submitChat(lastFailedDraft, false)
  }, [isSending, lastFailedDraft, submitChat])

  const handleRetryIngest = useCallback(async () => {
    await handleIngest(lastIngestFile)
  }, [handleIngest, lastIngestFile])

  const handleNewChat = useCallback(() => {
    chatAbortRef.current?.abort()
    setConversationId(globalThis.crypto.randomUUID())
    setMessages([])
    setCitations([])
    setDraft('')
    setLastFailedDraft(null)
    setStatus({ tone: 'success', message: 'Started a new conversation.' })
  }, [])

  const badge = isOfflineMode ? 'Offline Mode' : 'GPT-4o-mini'
  const badgeClassName = isOfflineMode ? 'app-header-badge--offline' : undefined

  if (!isIngested) {
    return (
      <main className="app-shell app-shell--setup">
        <div className="setup-layout">
          <AppHeader badge={badge} badgeClassName={badgeClassName} />
          {status.message && (
            <StatusBanner ref={statusBannerRef} status={status} onDismiss={handleDismissStatus} />
          )}
          <IngestPanel onIngest={handleIngest} isBusy={isIngesting || isIngested === null} />
          {hasIngestToRetry && !isIngesting && (
            <button type="button" className="page-action-btn" onClick={handleRetryIngest}>
              Retry ingest
            </button>
          )}
        </div>
      </main>
    )
  }

  return (
    <main className="app-shell">
      <section className="chat-layout">
        <AppHeader badge={badge} badgeClassName={badgeClassName} onNewChat={handleNewChat} />
        {status.message && (
          <StatusBanner ref={statusBannerRef} status={status} onDismiss={handleDismissStatus} />
        )}
        <ChatTranscript
          messages={messages}
          isSending={isSending}
          onPromptSelect={handlePromptSelect}
        />
        <ChatComposer value={draft} onChange={setDraft} onSubmit={handleSend} isBusy={isSending} />
        {lastFailedDraft && (
          <div className="page-action-row">
            <button type="button" className="page-action-btn" onClick={handleRetryChat}>
              Retry last failed message
            </button>
          </div>
        )}
      </section>

      <aside className="sidebar">
        <CitationsPanel citations={citations} hasMessages={messages.length > 0} />
      </aside>
    </main>
  )
}
