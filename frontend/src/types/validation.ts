import { z } from 'zod'

export const CHAT_MAX_MESSAGES = 20
export const CHAT_MAX_MESSAGE_CONTENT_LENGTH = 4000
export const CHAT_MAX_CONVERSATION_ID_LENGTH = 128

export const ChatMessageSchema = z.object({
  role: z.enum(['user', 'assistant', 'system']),
  content: z.string().min(1).max(CHAT_MAX_MESSAGE_CONTENT_LENGTH),
  timestampUtc: z.iso.datetime(),
})

export const ChatRequestSchema = z
  .object({
    conversationId: z.string().min(1).max(CHAT_MAX_CONVERSATION_ID_LENGTH),
    messages: z.array(ChatMessageSchema).min(1).max(CHAT_MAX_MESSAGES),
  })
  .refine((request) => request.messages.some((message) => message.role === 'user'), {
    message: 'At least one user message is required.',
    path: ['messages'],
  })

export const IngestRequestSchema = z.object({
  forceReingest: z.boolean().default(false),
})
