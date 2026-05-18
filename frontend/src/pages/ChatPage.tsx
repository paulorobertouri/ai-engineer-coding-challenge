import { useCallback, useEffect, useRef, useState } from 'react'
import { apiClient } from '../services/apiClient'
import type {
  ChatMessage,
  Citation,
  ConfidenceIndicator,
  FeedbackKind,
  IngestResponse,
  ResponseFormat,
  ResponseLength,
  ResponseLanguage,
  ResponseTone,
  OperatorAuditDashboardResponse,
  RetrievalBenchmarkDashboardResponse,
  SourceComparisonResponse,
  SourceListItem,
  SourceDocumentResponse,
  SourceQualityReportResponse,
  StatusMessage,
} from '../types/chat'
import { AppHeader } from '../components/AppHeader'
import { ChatComposer } from '../components/chat/ChatComposer'
import { ChatTranscript } from '../components/chat/ChatTranscript'
import { CitationsPanel } from '../components/chat/CitationsPanel'
import { KeyboardShortcutMap } from '../components/KeyboardShortcutMap'
import { SourceDocumentViewer } from '../components/SourceDocumentViewer'
import { OperatorAuditPanel } from '../components/OperatorAuditPanel'
import { RetrievalBenchmarkPanel } from '../components/RetrievalBenchmarkPanel'
import { SourceQualityInspector } from '../components/SourceQualityInspector'
import { SourcesManagerPage } from '../components/SourcesManagerPage'
import { StatusBanner } from '../components/StatusBanner'

import { ChatRequestSchema, IngestRequestSchema, CHAT_MAX_MESSAGES } from '../types/validation'

const CHAT_SESSION_KEY = 'sop-assistant-chat-session-v1'

interface StoredChatSession {
  conversationId: string
  messages: ChatMessage[]
  citations: Citation[]
  confidence: ConfidenceIndicator | null
  responseLanguage?: ResponseLanguage
  responseTone?: ResponseTone
  responseLength?: ResponseLength
  responseFormat?: ResponseFormat
  followUpSuggestions?: string[]
}

function loadStoredChatSession(): StoredChatSession | null {
  try {
    const raw = sessionStorage.getItem(CHAT_SESSION_KEY)
    if (!raw) {
      return null
    }

    const stored = JSON.parse(raw) as Partial<StoredChatSession>
    return {
      conversationId:
        typeof stored.conversationId === 'string' && stored.conversationId.length > 0
          ? stored.conversationId
          : globalThis.crypto.randomUUID(),
      messages: Array.isArray(stored.messages) ? stored.messages : [],
      citations: Array.isArray(stored.citations) ? stored.citations : [],
      confidence:
        typeof stored.confidence === 'object' && stored.confidence !== null
          ? stored.confidence
          : null,
      responseLanguage:
        stored.responseLanguage === 'es' ||
        stored.responseLanguage === 'pt-BR' ||
        stored.responseLanguage === 'fr'
          ? stored.responseLanguage
          : 'en',
      responseTone:
        stored.responseTone === 'formal' || stored.responseTone === 'friendly'
          ? stored.responseTone
          : 'neutral',
      responseLength:
        stored.responseLength === 'short' || stored.responseLength === 'long'
          ? stored.responseLength
          : 'medium',
      responseFormat:
        stored.responseFormat === 'bullets' || stored.responseFormat === 'checklist'
          ? stored.responseFormat
          : 'paragraph',
      followUpSuggestions: Array.isArray(stored.followUpSuggestions)
        ? stored.followUpSuggestions.filter(
            (value) => typeof value === 'string' && value.trim().length > 0,
          )
        : [],
    }
  } catch {
    sessionStorage.removeItem(CHAT_SESSION_KEY)
    return null
  }
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

function appendStreamingDelta(
  currentMessages: ChatMessage[],
  assistantMessageId: string,
  delta: string,
): ChatMessage[] {
  return currentMessages.map((message) =>
    message.id === assistantMessageId ? { ...message, content: message.content + delta } : message,
  )
}

async function waitForIngestJobCompletion(
  jobId: string,
  signal: AbortSignal,
): Promise<IngestResponse> {
  const deadlineMs = Date.now() + 120000

  while (true) {
    if (signal.aborted) {
      throw new DOMException('Request cancelled.', 'AbortError')
    }

    const status = await apiClient.getIngestJobStatus(jobId, signal)
    if (status.status === 'succeeded') {
      if (!status.result) {
        throw new Error(status.message || 'Ingest job completed without a result.')
      }

      return status.result
    }

    if (status.status === 'failed') {
      throw new Error(status.errorMessage || status.message || 'Ingest job failed.')
    }

    if (Date.now() >= deadlineMs) {
      throw new Error('Timed out while waiting for ingest to complete.')
    }

    await new Promise((resolve) => {
      globalThis.window.setTimeout(resolve, 1000)
    })
  }
}

async function submitIngestRequest(
  file: File | undefined,
  signal: AbortSignal,
): Promise<IngestResponse> {
  if (file) {
    return apiClient.ingestFile(file, signal)
  }

  return apiClient.ingest(IngestRequestSchema.parse({ forceReingest: false }), signal)
}

function normalizeOptionalComment(comment: string | null | undefined): string | undefined {
  if (!comment) {
    return undefined
  }

  const trimmed = comment.trim()
  return trimmed.length > 0 ? trimmed : undefined
}

function buildConversationExport(
  conversationId: string,
  messages: ChatMessage[],
  citations: Citation[],
): string {
  const header = [
    '# SOP Assistant Conversation Export',
    '',
    `Conversation ID: ${conversationId}`,
    `Exported At (UTC): ${new Date().toISOString()}`,
    '',
    '## Transcript',
    '',
  ]

  const transcript = messages.flatMap((message) => [
    `### ${message.role.toUpperCase()} · ${message.timestamp}`,
    '',
    message.content,
    '',
  ])

  const evidenceHeader = ['## Cited Evidence', '']
  const evidence = citations.length
    ? citations.flatMap((citation, index) => [
        `### Citation ${index + 1}`,
        `- Source: ${citation.source}`,
        `- Chunk ID: ${citation.chunkId ?? 'n/a'}`,
        `- Lines: ${citation.startLine ?? 'n/a'}-${citation.endLine ?? 'n/a'}`,
        `- Score: ${citation.score ?? 'n/a'}`,
        `- Snippet: ${citation.snippet}`,
        '',
      ])
    : ['No citations captured in this session.', '']

  return [...header, ...transcript, ...evidenceHeader, ...evidence].join('\n')
}

function replaceStreamingMessage(
  currentMessages: ChatMessage[],
  assistantMessageId: string,
  content: string,
): ChatMessage[] {
  return currentMessages.map((message) =>
    message.id === assistantMessageId ? { ...message, content } : message,
  )
}

function replaceStreamingFailure(
  currentMessages: ChatMessage[],
  assistantMessageId: string,
): ChatMessage[] {
  return [
    ...currentMessages.filter((message) => message.id !== assistantMessageId),
    createMessage('assistant', 'The chat request failed. Start the backend and try again.'),
  ]
}

function shouldRetryHealthCheck(error: unknown): boolean {
  return error instanceof Error && error.message === 'Failed to fetch'
}

function buildHealthFailureStatus(error: unknown): StatusMessage {
  if (shouldRetryHealthCheck(error)) {
    return {
      tone: 'info',
      message: 'Backend health check failed: Failed to fetch. Retrying in 5s…',
    }
  }

  return {
    tone: 'warning',
    message:
      error instanceof Error
        ? `Backend health check failed: ${error.message}`
        : 'Backend health check failed.',
  }
}

export function ChatPage() {
  const [initialSession] = useState<StoredChatSession | null>(() => loadStoredChatSession())
  const [conversationId, setConversationId] = useState<string>(
    () => initialSession?.conversationId ?? globalThis.crypto.randomUUID(),
  )
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isIngesting, setIsIngesting] = useState(false)
  const [isOfflineMode, setIsOfflineMode] = useState(false)
  const [lastFailedDraft, setLastFailedDraft] = useState<string | null>(null)
  const [hasIngestToRetry, setHasIngestToRetry] = useState(false)
  const [lastIngestFile, setLastIngestFile] = useState<File | undefined>(undefined)
  const [citations, setCitations] = useState<Citation[]>(() => initialSession?.citations ?? [])
  const [confidence, setConfidence] = useState<ConfidenceIndicator | null>(
    () => initialSession?.confidence ?? null,
  )
  const [status, setStatus] = useState<StatusMessage>({
    tone: 'info',
    message: 'Checking backend health…',
  })
  const [sourceDocument, setSourceDocument] = useState<SourceDocumentResponse | null>(null)
  const [sourceComparison, setSourceComparison] = useState<SourceComparisonResponse | null>(null)
  const [sourceQuality, setSourceQuality] = useState<SourceQualityReportResponse | null>(null)
  const [isShortcutMapOpen, setIsShortcutMapOpen] = useState(false)
  const [isSourcesManagerOpen, setIsSourcesManagerOpen] = useState(false)
  const [sources, setSources] = useState<SourceListItem[]>([])
  const [isSourcesLoading, setIsSourcesLoading] = useState(false)
  const [sourceBeingRemoved, setSourceBeingRemoved] = useState<string | null>(null)
  const [pendingSourceRemoval, setPendingSourceRemoval] = useState<string | null>(null)
  const [auditDashboard, setAuditDashboard] = useState<OperatorAuditDashboardResponse | null>(null)
  const [isAuditLoading, setIsAuditLoading] = useState(false)
  const [auditError, setAuditError] = useState<string | null>(null)
  const [benchmarkDashboard, setBenchmarkDashboard] =
    useState<RetrievalBenchmarkDashboardResponse | null>(null)
  const [isBenchmarkLoading, setIsBenchmarkLoading] = useState(false)
  const [benchmarkError, setBenchmarkError] = useState<string | null>(null)
  const [auditFeedbackFilter, setAuditFeedbackFilter] = useState<
    '' | 'helpful' | 'unhelpful' | 'wrong-citation'
  >('')
  const [sourceDocumentError, setSourceDocumentError] = useState<string | null>(null)
  const [sourceQualityError, setSourceQualityError] = useState<string | null>(null)
  const [isSourceDocumentLoading, setIsSourceDocumentLoading] = useState(false)
  const [isSourceQualityLoading, setIsSourceQualityLoading] = useState(false)
  const [selectedCitation, setSelectedCitation] = useState<Citation | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>(() => initialSession?.messages ?? [])
  const [followUpSuggestions, setFollowUpSuggestions] = useState<string[]>(
    () => initialSession?.followUpSuggestions ?? [],
  )
  const [responseLanguage, setResponseLanguage] = useState<ResponseLanguage>(
    () => initialSession?.responseLanguage ?? 'en',
  )
  const [responseTone, setResponseTone] = useState<ResponseTone>(
    () => initialSession?.responseTone ?? 'neutral',
  )
  const [responseLength, setResponseLength] = useState<ResponseLength>(
    () => initialSession?.responseLength ?? 'medium',
  )
  const [responseFormat, setResponseFormat] = useState<ResponseFormat>(
    () => initialSession?.responseFormat ?? 'paragraph',
  )
  const chatAbortRef = useRef<AbortController | null>(null)
  const ingestAbortRef = useRef<AbortController | null>(null)
  const sourceAbortRef = useRef<AbortController | null>(null)
  const removeSourceTimeoutRef = useRef<number | null>(null)
  const sourceDocumentCacheRef = useRef<Map<string, SourceDocumentResponse>>(new Map())
  const statusBannerRef = useRef<HTMLElement | null>(null)
  // null = health check in progress; false = not yet ingested; true = ingested
  const [isIngested, setIsIngested] = useState<boolean | null>(null)

  useEffect(() => {
    const snapshot: StoredChatSession = {
      conversationId,
      messages,
      citations,
      confidence,
      responseLanguage,
      responseTone,
      responseLength,
      responseFormat,
      followUpSuggestions,
    }
    sessionStorage.setItem(CHAT_SESSION_KEY, JSON.stringify(snapshot))
  }, [
    conversationId,
    messages,
    citations,
    confidence,
    responseLanguage,
    responseTone,
    responseLength,
    responseFormat,
    followUpSuggestions,
  ])

  useEffect(() => {
    return () => {
      chatAbortRef.current?.abort()
      ingestAbortRef.current?.abort()
      sourceAbortRef.current?.abort()
      if (removeSourceTimeoutRef.current !== null) {
        globalThis.clearTimeout(removeSourceTimeoutRef.current)
        removeSourceTimeoutRef.current = null
      }
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

  const loadSources = useCallback(async () => {
    setIsSourcesLoading(true)
    try {
      const sourceItems = await apiClient.listSources()
      setSources(sourceItems)
      if (sourceItems.length > 0) {
        setIsIngested(true)
      }
    } catch {
      setSources([])
    } finally {
      setIsSourcesLoading(false)
    }
  }, [])

  useEffect(() => {
    let isCancelled = false

    async function loadHealth() {
      try {
        const health = await apiClient.getHealth()

        if (!isCancelled) {
          const isFallbackMode = health.notes.some((note) =>
            /fallback|no openai api key/i.test(note),
          )
          const hasIndexedSources = health.isIngested || health.recordCount > 0
          setIsOfflineMode(isFallbackMode)
          setIsIngested(hasIndexedSources)

          let statusTone: StatusMessage['tone'] = isFallbackMode ? 'warning' : 'success'
          let statusMessage = `${health.service} is running. ${health.notes[0] ?? ''}`.trim()

          try {
            const updateAlert = await apiClient.getSourceUpdateAlert()
            if (!isCancelled && updateAlert.requiresReingestReview) {
              statusTone = 'warning'
              statusMessage = `${health.service} is running. ${updateAlert.message}`
            }
          } catch {
            // Update alerts can be permission-gated; do not treat this as backend health failure.
          }

          setStatus({
            tone: statusTone,
            message: statusMessage,
          })
        }
      } catch (error) {
        if (!isCancelled) {
          setStatus(buildHealthFailureStatus(error))

          if (shouldRetryHealthCheck(error)) {
            setTimeout(() => {
              if (!isCancelled) {
                void loadHealth()
              }
            }, 5000)
          }
        }
      }
    }

    const initialLoadTimer = globalThis.setTimeout(() => {
      void loadHealth()
      void loadSources()
    }, 0)

    return () => {
      isCancelled = true
      globalThis.clearTimeout(initialLoadTimer)
    }
  }, [loadSources])

  const loadOperatorAudit = useCallback(async () => {
    setIsAuditLoading(true)
    setAuditError(null)

    try {
      const dashboard = await apiClient.getOperatorAuditDashboard({
        feedbackType: auditFeedbackFilter || undefined,
        lookbackHours: 24 * 7,
      })
      setAuditDashboard(dashboard)
    } catch (error) {
      setAuditDashboard(null)
      setAuditError(error instanceof Error ? error.message : 'Failed to load operator audit data.')
    } finally {
      setIsAuditLoading(false)
    }
  }, [auditFeedbackFilter])

  useEffect(() => {
    const timer = globalThis.setTimeout(() => {
      void loadOperatorAudit()
    }, 0)

    return () => {
      globalThis.clearTimeout(timer)
    }
  }, [loadOperatorAudit])

  const loadRetrievalBenchmarks = useCallback(async () => {
    setIsBenchmarkLoading(true)
    setBenchmarkError(null)
    try {
      const dashboard = await apiClient.getRetrievalBenchmarkDashboard(20)
      setBenchmarkDashboard(dashboard)
    } catch (error) {
      setBenchmarkDashboard(null)
      setBenchmarkError(
        error instanceof Error ? error.message : 'Failed to load retrieval benchmarks.',
      )
    } finally {
      setIsBenchmarkLoading(false)
    }
  }, [])

  useEffect(() => {
    const timer = globalThis.setTimeout(() => {
      void loadRetrievalBenchmarks()
    }, 0)

    return () => {
      globalThis.clearTimeout(timer)
    }
  }, [loadRetrievalBenchmarks])

  const handleRunRetrievalBenchmark = useCallback(async () => {
    setIsBenchmarkLoading(true)
    setBenchmarkError(null)
    try {
      await apiClient.runRetrievalBenchmark()
      await loadRetrievalBenchmarks()
      setStatus({ tone: 'success', message: 'Retrieval benchmark run completed.' })
    } catch (error) {
      setBenchmarkError(
        error instanceof Error ? error.message : 'Failed to run retrieval benchmark.',
      )
    } finally {
      setIsBenchmarkLoading(false)
    }
  }, [loadRetrievalBenchmarks])

  const handleIngest = useCallback(
    async (file?: File) => {
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
        const initialResponse = await submitIngestRequest(file, abortController.signal)
        let response = initialResponse
        if (initialResponse.jobId && initialResponse.jobStatusUrl) {
          setStatus({
            tone: 'info',
            message: `${initialResponse.message} Waiting for the job to finish…`,
          })
          response = await waitForIngestJobCompletion(initialResponse.jobId, abortController.signal)
        }

        setIsIngested(true)
        setHasIngestToRetry(false)
        setLastIngestFile(undefined)
        void loadSources()
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
          void loadSources()
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
    },
    [loadSources],
  )

  const submitChat = useCallback(
    async (messageText: string, clearDraft: boolean) => {
      chatAbortRef.current?.abort()
      const abortController = new AbortController()
      chatAbortRef.current = abortController

      const userMessage = createMessage('user', messageText)
      const requestMessages = [...messages, userMessage]
      const streamingAssistantMessage = createMessage('assistant', '')
      const nextMessages = [...requestMessages, streamingAssistantMessage]

      setMessages(nextMessages)
      setSelectedCitation(null)
      setSourceDocument(null)
      setSourceComparison(null)
      setSourceQuality(null)
      setSourceDocumentError(null)
      setSourceQualityError(null)
      if (clearDraft) {
        setDraft('')
      }
      setIsSending(true)
      setStatus({ tone: 'info', message: 'Sending chat request…' })

      try {
        // Limit history sent to the API to avoid unbounded token growth
        const historyWindow = requestMessages.slice(-CHAT_MAX_MESSAGES)

        const payload = ChatRequestSchema.parse({
          conversationId,
          responseLanguage,
          responseTone,
          responseLength,
          responseFormat,
          messages: historyWindow.map((message) => ({
            role: message.role,
            content: message.content,
            timestampUtc: message.timestamp,
          })),
        })

        const response =
          typeof apiClient.chatStream === 'function'
            ? await apiClient.chatStream(
                payload,
                {
                  onDelta: (delta) => {
                    setMessages((currentMessages) =>
                      appendStreamingDelta(currentMessages, streamingAssistantMessage.id, delta),
                    )
                  },
                },
                abortController.signal,
              )
            : await apiClient.chat(payload, abortController.signal)

        setMessages((currentMessages) =>
          replaceStreamingMessage(
            currentMessages,
            streamingAssistantMessage.id,
            response.assistantMessage,
          ),
        )
        setLastFailedDraft(null)
        setCitations(response.citations)
        setConfidence(response.confidence ?? null)
        setFollowUpSuggestions(response.structuredOutput?.followUpSuggestions ?? [])
        setStatus({
          tone: response.isPlaceholder ? 'warning' : 'success',
          message: `Chat response received with status '${response.status}'.`,
        })
      } catch (error) {
        if (isRequestCancelledError(error)) {
          setStatus({ tone: 'info', message: 'Chat request cancelled.' })
          return
        }

        setMessages((currentMessages) =>
          replaceStreamingFailure(currentMessages, streamingAssistantMessage.id),
        )
        setLastFailedDraft(messageText)
        setStatus({
          tone: 'error',
          message: error instanceof Error ? error.message : 'Chat request failed.',
        })
        setFollowUpSuggestions([])
      } finally {
        setIsSending(false)
        if (chatAbortRef.current === abortController) {
          chatAbortRef.current = null
        }
      }
    },
    [conversationId, messages, responseFormat, responseLanguage, responseLength, responseTone],
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

  /* c8 ignore start */
  const commitRemoveSource = useCallback(
    async (source: string) => {
      setSourceBeingRemoved(source)
      try {
        const result = await apiClient.deleteSource(source)
        await loadSources()
        setPendingSourceRemoval((current) => (current === source ? null : current))
        setStatus({ tone: 'success', message: result.message })
      } catch (error) {
        setStatus({
          tone: 'error',
          message: error instanceof Error ? error.message : 'Failed to remove source.',
        })
      } finally {
        setSourceBeingRemoved(null)
      }
    },
    [loadSources],
  )

  const handleRemoveSource = useCallback(
    (source: string) => {
      if (removeSourceTimeoutRef.current !== null) {
        globalThis.clearTimeout(removeSourceTimeoutRef.current)
        removeSourceTimeoutRef.current = null
      }

      setPendingSourceRemoval(source)
      setStatus({
        tone: 'warning',
        message: `"${source}" will be removed in 5 seconds. You can undo this action.`,
      })

      removeSourceTimeoutRef.current = globalThis.window.setTimeout(() => {
        removeSourceTimeoutRef.current = null
        void commitRemoveSource(source)
      }, 5000)
    },
    [commitRemoveSource],
  )

  const handleUndoRemoveSource = useCallback(() => {
    if (removeSourceTimeoutRef.current !== null) {
      globalThis.clearTimeout(removeSourceTimeoutRef.current)
      removeSourceTimeoutRef.current = null
    }

    if (pendingSourceRemoval) {
      setStatus({ tone: 'success', message: `Removal cancelled for "${pendingSourceRemoval}".` })
    }

    setPendingSourceRemoval(null)
  }, [pendingSourceRemoval])
  /* c8 ignore stop */

  const handleRetryChat = useCallback(async () => {
    if (!lastFailedDraft || isSending) {
      return
    }

    await submitChat(lastFailedDraft, false)
  }, [isSending, lastFailedDraft, submitChat])

  const handleRetryIngest = useCallback(async () => {
    await handleIngest(lastIngestFile)
  }, [handleIngest, lastIngestFile])

  const handleSubmitFeedback = useCallback(
    async (messageId: string, feedbackType: FeedbackKind) => {
      const optionalComment =
        feedbackType === 'helpful'
          ? undefined
          : globalThis.window.prompt('Optional feedback note (leave empty to skip):')

      const comment = normalizeOptionalComment(optionalComment)

      try {
        await apiClient.submitFeedback({
          conversationId,
          messageId,
          feedbackType,
          comment,
        })

        void loadOperatorAudit()

        setStatus({
          tone: 'success',
          message: 'Feedback submitted. Thank you.',
        })
      } catch (error) {
        setStatus({
          tone: 'error',
          message: error instanceof Error ? error.message : 'Failed to submit feedback.',
        })
      }
    },
    [conversationId, loadOperatorAudit],
  )

  const handleSelectCitation = useCallback(async (citation: Citation) => {
    setSelectedCitation(citation)

    const source = citation.source.trim()
    if (!source) {
      setSourceDocument(null)
      setSourceComparison(null)
      setSourceQuality(null)
      setSourceDocumentError('The selected citation does not include a source name.')
      setSourceQualityError(null)
      return
    }

    const cacheKey = `${citation.knowledgeBaseId ?? 'default'}::${source}`
    const cachedDocument = sourceDocumentCacheRef.current.get(cacheKey)
    if (cachedDocument) {
      setSourceDocument(cachedDocument)
      setSourceDocumentError(null)
    }

    sourceAbortRef.current?.abort()
    const abortController = new AbortController()
    sourceAbortRef.current = abortController

    setIsSourceDocumentLoading(true)
    setIsSourceQualityLoading(true)
    setSourceDocumentError(null)
    setSourceQualityError(null)

    try {
      const [document, comparison, quality] = await Promise.all([
        cachedDocument
          ? Promise.resolve(cachedDocument)
          : apiClient.getSourceDocument(source, citation.knowledgeBaseId, abortController.signal),
        apiClient.getSourceComparison(
          source,
          citation.knowledgeBaseId,
          citation.chunkId,
          abortController.signal,
        ),
        apiClient.getSourceQuality(source, citation.knowledgeBaseId, abortController.signal),
      ])

      if (!cachedDocument) {
        sourceDocumentCacheRef.current.set(cacheKey, document)
      }

      setSourceDocument(document)
      setSourceComparison(comparison)
      setSourceQuality(quality)
    } catch (error) {
      if (!isRequestCancelledError(error)) {
        setSourceDocument(null)
        setSourceComparison(null)
        setSourceQuality(null)
        const message = error instanceof Error ? error.message : 'Failed to load source data.'
        setSourceDocumentError(message)
        setSourceQualityError(message)
      }
    } finally {
      setIsSourceDocumentLoading(false)
      setIsSourceQualityLoading(false)
      if (sourceAbortRef.current === abortController) {
        sourceAbortRef.current = null
      }
    }
  }, [])

  const handleNewChat = useCallback(() => {
    chatAbortRef.current?.abort()
    sourceAbortRef.current?.abort()
    setConversationId(globalThis.crypto.randomUUID())
    setMessages([])
    setCitations([])
    setConfidence(null)
    setFollowUpSuggestions([])
    setSelectedCitation(null)
    setSourceDocument(null)
    setSourceComparison(null)
    setSourceQuality(null)
    setSourceDocumentError(null)
    setSourceQualityError(null)
    setDraft('')
    setLastFailedDraft(null)
    setResponseLanguage('en')
    setResponseTone('neutral')
    setResponseLength('medium')
    setResponseFormat('paragraph')
    setStatus({ tone: 'success', message: 'Started a new conversation.' })
  }, [])

  const handleOpenSourcesManager = useCallback(() => {
    setIsSourcesManagerOpen(true)
  }, [])

  const handleOpenChat = useCallback(() => {
    setIsSourcesManagerOpen(false)
  }, [])

  const handleExportConversation = useCallback(() => {
    if (messages.length === 0) {
      setStatus({ tone: 'warning', message: 'No transcript is available to export yet.' })
      return
    }

    const exportText = buildConversationExport(conversationId, messages, citations)
    const blob = new Blob([exportText], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `conversation-${conversationId}.md`
    anchor.click()
    URL.revokeObjectURL(url)

    setStatus({ tone: 'success', message: 'Conversation export downloaded.' })
  }, [citations, conversationId, messages])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      const isTypingTarget =
        target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA' || target?.isContentEditable

      if (event.key === '?' && !event.ctrlKey && !event.metaKey && !event.altKey) {
        event.preventDefault()
        setIsShortcutMapOpen(true)
        return
      }

      if (event.key === 'Escape' && isShortcutMapOpen) {
        event.preventDefault()
        setIsShortcutMapOpen(false)
        return
      }

      if (isTypingTarget) {
        return
      }

      if (event.key === '/' && !event.ctrlKey && !event.metaKey && !event.altKey) {
        event.preventDefault()
        const input = document.getElementById('chat-input') as HTMLTextAreaElement | null
        input?.focus()
        return
      }

      if (event.altKey && (event.key === 'n' || event.key === 'N')) {
        event.preventDefault()
        handleNewChat()
        return
      }

      if (event.altKey && (event.key === 'e' || event.key === 'E')) {
        event.preventDefault()
        handleExportConversation()
      }
    }

    globalThis.addEventListener('keydown', onKeyDown)
    return () => globalThis.removeEventListener('keydown', onKeyDown)
  }, [handleExportConversation, handleNewChat, isShortcutMapOpen])

  const badge = isOfflineMode ? 'Offline Mode' : 'GPT-5.4-mini'
  const badgeClassName = isOfflineMode ? 'app-header-badge--offline' : undefined

  if (!isIngested || isSourcesManagerOpen) {
    return (
      <main className="app-shell app-shell--setup">
        <div className="setup-layout">
          <AppHeader
            badge={badge}
            badgeClassName={badgeClassName}
            onNewChat={isIngested ? handleOpenChat : undefined}
          />
          {status.message && (
            <StatusBanner ref={statusBannerRef} status={status} onDismiss={handleDismissStatus} />
          )}
          <SourcesManagerPage
            sources={sources}
            isLoadingSources={isSourcesLoading}
            sourceBeingRemoved={sourceBeingRemoved}
            isIngesting={isIngesting || isIngested === null}
            onIngest={handleIngest}
            onRefresh={() => {
              void loadSources()
            }}
            onRemove={handleRemoveSource}
            canOpenChat={isIngested === true}
            onOpenChat={handleOpenChat}
          />
          {/* c8 ignore next 7 */}
          {pendingSourceRemoval && sourceBeingRemoved !== pendingSourceRemoval && (
            <div className="page-action-row">
              <button type="button" className="page-action-btn" onClick={handleUndoRemoveSource}>
                Undo remove {pendingSourceRemoval}
              </button>
            </div>
          )}
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
        <div className="page-action-row u-mt-1">
          <button type="button" className="page-action-btn" onClick={handleOpenSourcesManager}>
            Manage sources
          </button>
          <button type="button" className="page-action-btn" onClick={handleExportConversation}>
            Export conversation
          </button>
          <button
            type="button"
            className="page-action-btn"
            onClick={() => setIsShortcutMapOpen(true)}
          >
            Keyboard shortcuts
          </button>
        </div>
        <ChatTranscript
          messages={messages}
          isSending={isSending}
          onPromptSelect={handlePromptSelect}
          onFeedbackSubmit={handleSubmitFeedback}
        />
        <ChatComposer
          value={draft}
          onChange={setDraft}
          onSubmit={handleSend}
          isBusy={isSending}
          responseLanguage={responseLanguage}
          onResponseLanguageChange={setResponseLanguage}
          responseTone={responseTone}
          onResponseToneChange={setResponseTone}
          responseLength={responseLength}
          onResponseLengthChange={setResponseLength}
          responseFormat={responseFormat}
          onResponseFormatChange={setResponseFormat}
        />
        {lastFailedDraft && (
          <div className="page-action-row">
            <button type="button" className="page-action-btn" onClick={handleRetryChat}>
              Retry last failed message
            </button>
          </div>
        )}
      </section>

      <aside className="sidebar">
        <CitationsPanel
          citations={citations}
          confidence={confidence}
          hasMessages={messages.length > 0}
          onSelectCitation={handleSelectCitation}
        />
        <SourceDocumentViewer
          document={sourceDocument}
          comparison={sourceComparison}
          activeCitation={selectedCitation}
          isLoading={isSourceDocumentLoading}
          errorMessage={sourceDocumentError}
        />
        <SourceQualityInspector
          report={sourceQuality}
          isLoading={isSourceQualityLoading}
          errorMessage={sourceQualityError}
        />
        <OperatorAuditPanel
          dashboard={auditDashboard}
          isLoading={isAuditLoading}
          errorMessage={auditError}
          feedbackFilter={auditFeedbackFilter}
          onFeedbackFilterChange={setAuditFeedbackFilter}
          onRefresh={() => {
            void loadOperatorAudit()
          }}
        />
        <RetrievalBenchmarkPanel
          dashboard={benchmarkDashboard}
          isLoading={isBenchmarkLoading}
          errorMessage={benchmarkError}
          onRun={() => {
            void handleRunRetrievalBenchmark()
          }}
        />
      </aside>

      {isShortcutMapOpen && <KeyboardShortcutMap onClose={() => setIsShortcutMapOpen(false)} />}
    </main>
  )
}
