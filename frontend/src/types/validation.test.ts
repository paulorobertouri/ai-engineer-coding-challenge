import { describe, it, expect } from 'vitest'
import { ChatMessageSchema, ChatRequestSchema, IngestRequestSchema } from './validation'

describe('ChatMessageSchema', () => {
  const validTimestamp = new Date().toISOString()

  it('accepts a valid user message', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'user',
      content: 'hello',
      timestampUtc: validTimestamp,
    })
    expect(result.success).toBe(true)
  })

  it('accepts assistant role', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'assistant',
      content: 'hi',
      timestampUtc: validTimestamp,
    })
    expect(result.success).toBe(true)
  })

  it('accepts system role', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'system',
      content: 'you are helpful',
      timestampUtc: validTimestamp,
    })
    expect(result.success).toBe(true)
  })

  it('rejects an invalid role', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'admin',
      content: 'hello',
      timestampUtc: validTimestamp,
    })
    expect(result.success).toBe(false)
  })

  it('rejects empty content', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'user',
      content: '',
      timestampUtc: validTimestamp,
    })
    expect(result.success).toBe(false)
  })

  it('rejects invalid timestamp format', () => {
    const result = ChatMessageSchema.safeParse({
      role: 'user',
      content: 'hi',
      timestampUtc: 'not-a-date',
    })
    expect(result.success).toBe(false)
  })
})

describe('ChatRequestSchema', () => {
  const validTimestamp = new Date().toISOString()

  it('accepts a valid chat request', () => {
    const result = ChatRequestSchema.safeParse({
      conversationId: 'abc',
      useTools: true,
      messages: [{ role: 'user', content: 'hi', timestampUtc: validTimestamp }],
    })
    expect(result.success).toBe(true)
  })

  it('rejects empty messages array', () => {
    const result = ChatRequestSchema.safeParse({
      conversationId: 'abc',
      useTools: false,
      messages: [],
    })
    expect(result.success).toBe(false)
  })

  it('rejects missing conversationId', () => {
    const result = ChatRequestSchema.safeParse({
      useTools: true,
      messages: [{ role: 'user', content: 'hi', timestampUtc: validTimestamp }],
    })
    expect(result.success).toBe(false)
  })
})

describe('IngestRequestSchema', () => {
  it('accepts a valid ingest request', () => {
    const result = IngestRequestSchema.safeParse({
      sourcePath: '/path/to/file.md',
      forceReingest: false,
    })
    expect(result.success).toBe(true)
  })

  it('rejects empty sourcePath', () => {
    const result = IngestRequestSchema.safeParse({ sourcePath: '', forceReingest: false })
    expect(result.success).toBe(false)
  })

  it('defaults forceReingest to false when omitted', () => {
    const result = IngestRequestSchema.safeParse({ sourcePath: '/path/file.md' })
    expect(result.success).toBe(true)
    if (result.success) expect(result.data.forceReingest).toBe(false)
  })

  it('accepts forceReingest true', () => {
    const result = IngestRequestSchema.safeParse({
      sourcePath: '/path/file.md',
      forceReingest: true,
    })
    expect(result.success).toBe(true)
    if (result.success) expect(result.data.forceReingest).toBe(true)
  })
})
