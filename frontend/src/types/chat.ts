export type ChatRole = 'user' | 'assistant' | 'system' | 'tool'

export type StatusTone = 'info' | 'success' | 'warning' | 'error'

export interface ChatMessage {
  id: string
  role: ChatRole
  content: string
  timestamp: string
}

export interface Citation {
  chunkId?: string
  knowledgeBaseId?: string
  documentVersion?: string
  sourceChecksum?: string
  ingestedAtUtc?: string
  source: string
  sectionTitle?: string
  snippet: string
  score?: number
  startLine?: number
  endLine?: number
}

export interface ChatApiMessage {
  role: ChatRole
  content: string
  timestampUtc: string
}

export interface ChatRequest {
  conversationId: string
  knowledgeBaseId?: string
  messages: ChatApiMessage[]
}

export type FeedbackKind = 'helpful' | 'unhelpful' | 'wrong-citation'

export interface ConversationFeedbackRequest {
  conversationId: string
  messageId: string
  feedbackType: FeedbackKind
  comment?: string
}

export interface ConversationFeedbackResponse {
  accepted: boolean
  message: string
  submittedAtUtc: string
}

export interface ChatResponse {
  conversationId: string
  assistantMessage: string
  status: string
  isPlaceholder: boolean
  toolCalls: string[]
  citations: Citation[]
  confidence?: ConfidenceIndicator
  usage?: {
    model: string
    source: string
    isEstimated: boolean
    promptTokens: number
    completionTokens: number
    embeddingTokens: number
    totalTokens: number
    estimatedCostUsd: number
  }
  structuredOutput?: {
    answerText: string
    citedChunkIds: string[]
    refusalReason?: string
    followUpSuggestions: string[]
  }
}

export type ConfidenceLevel = 'high' | 'medium' | 'low' | 'not_found'

export interface ConfidenceIndicator {
  level: ConfidenceLevel
  evidenceCoverage: number
}

export interface IngestRequest {
  forceReingest: boolean
  knowledgeBaseId?: string
}

export interface IngestResponse {
  accepted: boolean
  message: string
  sourcePath: string
  chunksCreated: number
  recordsPersisted: number
  vectorStorePath: string
  knowledgeBaseId: string
  documentVersion?: string
  sourceChecksum?: string
  ingestedAtUtc?: string
  isPlaceholder: boolean
  jobId?: string
  jobStatus?: string
  jobStatusUrl?: string
}

export interface IngestJobStatusResponse {
  jobId: string
  knowledgeBaseId: string
  status: string
  message: string
  queuedAtUtc: string
  startedAtUtc?: string | null
  completedAtUtc?: string | null
  result?: IngestResponse | null
  errorMessage?: string | null
}

export interface SourceDocumentChunk {
  chunkId: string
  sectionTitle: string
  content: string
  startLine?: number
  endLine?: number
  index?: number
}

export interface SourceDocumentResponse {
  source: string
  knowledgeBaseId: string
  documentVersion?: string
  chunks: SourceDocumentChunk[]
}

export interface HealthResponse {
  status: string
  service: string
  utcTime: string
  notes: string[]
  isIngested: boolean
  recordCount: number
  activeKnowledgeBaseIds: string[]
}

export interface StatusMessage {
  tone: StatusTone
  message: string
}
