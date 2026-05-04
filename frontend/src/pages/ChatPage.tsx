import { useEffect, useState } from 'react'
import { ShoppingCart } from 'lucide-react'
import { apiClient } from '../services/apiClient'
import type { ChatMessage, Citation, StatusMessage } from '../types/chat'
import { ChatComposer } from '../components/ChatComposer'
import { ChatTranscript } from '../components/ChatTranscript'
import { CitationsPanel } from '../components/CitationsPanel'
import { IngestPanel } from '../components/IngestPanel'
import { StatusBanner } from '../components/StatusBanner'

import { ChatRequestSchema, IngestRequestSchema } from '../types/validation'

const defaultSourcePath = 'knowledge-base/Grocery_Store_SOP.md'

function createMessage(role: ChatMessage['role'], content: string): ChatMessage {
  return {
    id: window.crypto.randomUUID(),
    role,
    content,
    timestamp: new Date().toISOString(),
  }
}

export function ChatPage() {
  const [conversationId] = useState(() => window.crypto.randomUUID())
  const [draft, setDraft] = useState('')
  const [sourcePath, setSourcePath] = useState(defaultSourcePath)
  const [isSending, setIsSending] = useState(false)
  const [isIngesting, setIsIngesting] = useState(false)
  const [citations, setCitations] = useState<Citation[]>([])
  const [status, setStatus] = useState<StatusMessage>({
    tone: 'info',
    message: 'Checking backend health...',
  })
  const [messages, setMessages] = useState<ChatMessage[]>([])

  useEffect(() => {
    let isCancelled = false

    async function loadHealth() {
      try {
        const health = await apiClient.getHealth()

        if (!isCancelled) {
          setStatus({
            tone: 'success',
            message: `${health.service} is running. ${health.notes[0] ?? ''}`.trim(),
          })
        }
      } catch (error) {
        if (!isCancelled) {
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

    void loadHealth()

    return () => {
      isCancelled = true
    }
  }, [])

  async function handleIngest() {
    setIsIngesting(true)
    setStatus({ tone: 'info', message: 'Calling the ingest endpoint...' })

    try {
      const payload = IngestRequestSchema.parse({
        sourcePath,
        forceReingest: false,
      })

      const response = await apiClient.ingest(payload)

      setStatus({
        tone: response.isPlaceholder ? 'warning' : 'success',
        message: `${response.message} Vector store: ${response.vectorStorePath}`,
      })
    } catch (error) {
      setStatus({
        tone: 'error',
        message: error instanceof Error ? error.message : 'Ingest request failed.',
      })
    } finally {
      setIsIngesting(false)
    }
  }

  async function handleSend() {
    const trimmedDraft = draft.trim()
    if (!trimmedDraft) {
      return
    }

    const userMessage = createMessage('user', trimmedDraft)
    const nextMessages = [...messages, userMessage]

    setMessages(nextMessages)
    setDraft('')
    setIsSending(true)
    setStatus({ tone: 'info', message: 'Sending chat request...' })

    try {
      const payload = ChatRequestSchema.parse({
        conversationId,
        useTools: true,
        messages: nextMessages.map((message) => ({
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
  }

  return (
    <main className="app-shell">
      <section className="chat-layout">
        <header className="app-header">
          <div className="app-header-inner">
            <div className="app-header-icon">
              <ShoppingCart size={15} color="white" strokeWidth={2.5} />
            </div>
            <div className="app-header-text">
              <h1>SOP Assistant</h1>
              <p>Grocery Store Operating Procedures · Powered by AI</p>
            </div>
            <span className="app-header-badge">GPT-5-mini</span>
          </div>
        </header>
        <StatusBanner status={status} />
        <ChatTranscript messages={messages} />
        <ChatComposer value={draft} onChange={setDraft} onSubmit={handleSend} isBusy={isSending} />
      </section>

      <aside className="sidebar">
        <IngestPanel
          sourcePath={sourcePath}
          onSourcePathChange={setSourcePath}
          onIngest={handleIngest}
          isBusy={isIngesting}
        />
        <CitationsPanel citations={citations} />
      </aside>
    </main>
  )
}
