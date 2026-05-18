import type {
  ChatMessageDto as ContractChatMessageDto,
  ChatRequest as ContractChatRequest,
  ChatResponse as ContractChatResponse,
  CitationDto as ContractCitationDto,
  ConfidenceIndicatorDto as ContractConfidenceIndicatorDto,
  ConversationFeedbackRequest as ContractConversationFeedbackRequest,
  ConversationFeedbackResponse as ContractConversationFeedbackResponse,
  HealthResponse as ContractHealthResponse,
  IngestJobStatusResponse as ContractIngestJobStatusResponse,
  IngestRequest as ContractIngestRequest,
  IngestResponse as ContractIngestResponse,
  SourceDocumentChunkDto as ContractSourceDocumentChunkDto,
  SourceDocumentResponse as ContractSourceDocumentResponse,
} from '../generated/api-types'

export type ChatRole = 'user' | 'assistant' | 'system' | 'tool'
export type ResponseLanguage = 'en' | 'es' | 'pt-BR' | 'fr'
export type ResponseTone = 'neutral' | 'formal' | 'friendly'
export type ResponseLength = 'short' | 'medium' | 'long'
export type ResponseFormat = 'paragraph' | 'bullets' | 'checklist'

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
  userRole?: 'cashier' | 'manager' | 'department_lead'
  responseLanguage?: ResponseLanguage
  responseTone?: ResponseTone
  responseLength?: ResponseLength
  responseFormat?: ResponseFormat
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
  result?: IngestResponse
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

export interface SourceUpdateAlertResponse {
  knowledgeBaseId: string
  requiresReingestReview: boolean
  currentSourceChecksum?: string
  ingestedSourceChecksum?: string
  detectedAtUtc: string
  message: string
}

export interface SourceComparisonChunk {
  index?: number
  sectionTitle: string
  startLine?: number
  endLine?: number
  ingestedChunkId?: string
  currentChunkId?: string
  changeType: 'unchanged' | 'modified' | 'added' | 'removed'
  isImpactedCitation: boolean
  ingestedContent?: string
  currentContent?: string
}

export interface SourceComparisonResponse {
  source: string
  knowledgeBaseId: string
  ingestedDocumentVersion?: string
  currentDocumentVersion: string
  changedChunkCount: number
  totalComparedChunks: number
  chunks: SourceComparisonChunk[]
}

export interface SourceQualityOutlier {
  chunkId: string
  sectionTitle: string
  characterCount: number
  startLine?: number
  endLine?: number
}

export interface SourceQualityReportResponse {
  source: string
  knowledgeBaseId: string
  totalChunks: number
  duplicateSectionCount: number
  weakExtractionZoneCount: number
  shortestChunks: SourceQualityOutlier[]
  longestChunks: SourceQualityOutlier[]
}

export interface SourceListItem {
  source: string
  knowledgeBaseId: string
  chunkCount: number
  documentVersion?: string
  sourceChecksum?: string
  ingestedAtUtc?: string
}

export interface SourceDeleteResponse {
  source: string
  knowledgeBaseId: string
  removedChunks: number
  message: string
}

export type OperatorAuditSeverity = 'info' | 'warning' | 'error'

export interface OperatorAuditEntry {
  timestampUtc: string
  type: string
  severity: OperatorAuditSeverity
  conversationId: string
  messageId: string
  feedbackType: string
  comment?: string
  action: string
  outcome: string
  knowledgeBaseId: string
  sourceName: string
  safeSummary?: string
}

export interface OperatorAuditDashboardResponse {
  generatedAtUtc: string
  fromUtc: string
  toUtc: string
  knowledgeBaseId?: string
  feedbackTypeFilter?: string
  feedbackCount: number
  lowConfidenceSignalCount: number
  failedIngestCount: number
  feedback: OperatorAuditEntry[]
  lowConfidenceSignals: OperatorAuditEntry[]
  failedIngests: OperatorAuditEntry[]
}

export interface RetrievalBenchmarkEntry {
  runId: string
  timestampUtc: string
  commit: string
  fixtureCount: number
  precision: number
  recall: number
}

export interface RetrievalBenchmarkDashboardResponse {
  generatedAtUtc: string
  entries: RetrievalBenchmarkEntry[]
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

type IsAssignable<Source, Target> = [Source] extends [Target] ? true : false
type Assert<T extends true> = T

export type ApiContractCompatibilityChecks = [
  Assert<IsAssignable<ChatApiMessage, ContractChatMessageDto>>,
  Assert<IsAssignable<ChatRequest, ContractChatRequest>>,
  Assert<IsAssignable<Citation, ContractCitationDto>>,
  Assert<IsAssignable<ConfidenceIndicator, ContractConfidenceIndicatorDto>>,
  Assert<IsAssignable<ChatResponse, ContractChatResponse>>,
  Assert<IsAssignable<ConversationFeedbackRequest, ContractConversationFeedbackRequest>>,
  Assert<IsAssignable<ConversationFeedbackResponse, ContractConversationFeedbackResponse>>,
  Assert<IsAssignable<IngestRequest, ContractIngestRequest>>,
  Assert<IsAssignable<IngestResponse, ContractIngestResponse>>,
  Assert<IsAssignable<IngestJobStatusResponse, ContractIngestJobStatusResponse>>,
  Assert<IsAssignable<SourceDocumentChunk, ContractSourceDocumentChunkDto>>,
  Assert<IsAssignable<SourceDocumentResponse, ContractSourceDocumentResponse>>,
  Assert<IsAssignable<HealthResponse, ContractHealthResponse>>,
]
