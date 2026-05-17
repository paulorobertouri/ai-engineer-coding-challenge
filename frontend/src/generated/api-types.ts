/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export interface ChatMessageDto {
  /**
   * @minLength 1
   * @pattern ^(user|assistant|system)$
   */
  role: string
  /**
   * @minLength 1
   * @maxLength 4000
   */
  content: string
  /** @format date-time */
  timestampUtc?: string
}

export interface ChatRequest {
  /**
   * @minLength 1
   * @maxLength 128
   */
  conversationId: string
  /**
   * @maxItems 20
   * @minItems 1
   */
  messages: ChatMessageDto[]
  /**
   * @maxLength 64
   * @pattern ^[a-zA-Z0-9._-]*$
   */
  knowledgeBaseId?: string | null
  useTools?: boolean
}

export interface ChatResponse {
  conversationId?: string | null
  assistantMessage?: string | null
  status?: string | null
  isPlaceholder?: boolean
  toolCalls?: string[] | null
  citations?: CitationDto[] | null
  structuredOutput?: StructuredAnswerDto
  confidence?: ConfidenceIndicatorDto
  usage?: ChatUsageDto
}

export interface ChatUsageDto {
  model?: string | null
  source?: string | null
  isEstimated?: boolean
  /** @format int32 */
  promptTokens?: number
  /** @format int32 */
  completionTokens?: number
  /** @format int32 */
  embeddingTokens?: number
  /** @format int32 */
  totalTokens?: number
  /** @format double */
  estimatedCostUsd?: number
}

export interface CitationDto {
  chunkId?: string | null
  knowledgeBaseId?: string | null
  documentVersion?: string | null
  sourceChecksum?: string | null
  /** @format date-time */
  ingestedAtUtc?: string | null
  source?: string | null
  sectionTitle?: string | null
  snippet?: string | null
  /** @format double */
  score?: number | null
  /** @format int32 */
  startLine?: number | null
  /** @format int32 */
  endLine?: number | null
}

export interface ConfidenceIndicatorDto {
  level?: string | null
  /** @format double */
  evidenceCoverage?: number
}

export interface ConversationFeedbackRequest {
  /**
   * @minLength 1
   * @maxLength 128
   */
  conversationId: string
  /**
   * @minLength 1
   * @maxLength 128
   */
  messageId: string
  /**
   * @minLength 1
   * @pattern ^(helpful|unhelpful|wrong-citation)$
   */
  feedbackType: string
  /** @maxLength 500 */
  comment?: string | null
}

export interface ConversationFeedbackResponse {
  accepted?: boolean
  message?: string | null
  /** @format date-time */
  submittedAtUtc?: string
}

export interface HealthResponse {
  status?: string | null
  service?: string | null
  /** @format date-time */
  utcTime?: string
  notes?: string[] | null
  isIngested?: boolean
  /** @format int32 */
  recordCount?: number
  activeKnowledgeBaseIds?: string[] | null
}

export interface IngestJobStatusResponse {
  /** @format uuid */
  jobId?: string
  knowledgeBaseId?: string | null
  status?: string | null
  message?: string | null
  /** @format date-time */
  queuedAtUtc?: string
  /** @format date-time */
  startedAtUtc?: string | null
  /** @format date-time */
  completedAtUtc?: string | null
  result?: IngestResponse
  errorMessage?: string | null
}

export interface IngestPreviewChunk {
  id?: string | null
  sectionTitle?: string | null
  /** @format int32 */
  characterCount?: number
  sampleText?: string | null
}

export interface IngestPreviewResponse {
  accepted?: boolean
  message?: string | null
  sourceName?: string | null
  /** @format int32 */
  chunkCount?: number
  chunks?: IngestPreviewChunk[] | null
}

export interface IngestRequest {
  forceReingest?: boolean
  /**
   * @maxLength 64
   * @pattern ^[a-zA-Z0-9._-]*$
   */
  knowledgeBaseId?: string | null
}

export interface IngestResponse {
  accepted?: boolean
  message?: string | null
  sourcePath?: string | null
  /** @format int32 */
  chunksCreated?: number
  /** @format int32 */
  recordsPersisted?: number
  vectorStorePath?: string | null
  knowledgeBaseId?: string | null
  documentVersion?: string | null
  sourceChecksum?: string | null
  /** @format date-time */
  ingestedAtUtc?: string
  isPlaceholder?: boolean
  /** @format uuid */
  jobId?: string | null
  jobStatus?: string | null
  jobStatusUrl?: string | null
}

export interface SourceDocumentChunkDto {
  chunkId?: string | null
  sectionTitle?: string | null
  content?: string | null
  /** @format int32 */
  startLine?: number | null
  /** @format int32 */
  endLine?: number | null
  /** @format int32 */
  index?: number | null
}

export interface SourceDocumentResponse {
  source?: string | null
  knowledgeBaseId?: string | null
  documentVersion?: string | null
  chunks?: SourceDocumentChunkDto[] | null
}

export interface StructuredAnswerDto {
  answerText?: string | null
  citedChunkIds?: string[] | null
  refusalReason?: string | null
  followUpSuggestions?: string[] | null
}

export type V1ChatCreateData = ChatResponse

export type V1ChatStreamCreateData = any

export type V1ChatFeedbackCreateData = ConversationFeedbackResponse

export type V1HealthListData = HealthResponse

export type V1ReadyListData = HealthResponse

export type V1IngestCreateData = IngestResponse

export interface V1IngestUploadCreatePayload {
  /** @format binary */
  file?: File
}

export interface V1IngestUploadCreateParams {
  knowledgeBaseId?: string
}

export type V1IngestUploadCreateData = IngestResponse

export interface V1IngestJobsDetailParams {
  /** @format uuid */
  jobId: string
}

export type V1IngestJobsDetailData = IngestJobStatusResponse

export interface V1IngestPreviewCreatePayload {
  /** @format binary */
  file?: File
}

export type V1IngestPreviewCreateData = IngestPreviewResponse

export interface V1IngestResetDeleteParams {
  confirm?: string
}

export type V1IngestResetDeleteData = any

export interface V1SourcesDocumentListParams {
  source?: string
  knowledgeBaseId?: string
}

export type V1SourcesDocumentListData = SourceDocumentResponse
