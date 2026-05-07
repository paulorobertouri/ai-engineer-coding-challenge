import { z } from 'zod'

export const ChatMessageSchema = z.object({
  role: z.enum(['user', 'assistant', 'system']),
  content: z.string().min(1),
  timestampUtc: z.iso.datetime(),
})

export const ChatRequestSchema = z.object({
  conversationId: z.string().min(1),
  useTools: z.boolean(),
  messages: z.array(ChatMessageSchema).min(1),
})

export const IngestRequestSchema = z.object({
  forceReingest: z.boolean().default(false),
})
