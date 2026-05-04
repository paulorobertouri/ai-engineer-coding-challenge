import { render, screen } from '@testing-library/react'
import { ChatTranscript } from './ChatTranscript'
import { describe, it, expect } from 'vitest'
import type { ChatMessage } from '../types/chat'

describe('ChatTranscript', () => {
  const mockMessages: ChatMessage[] = [
    {
      id: '1',
      role: 'user',
      content: 'Hello assistant',
      timestamp: new Date().toISOString(),
    },
    {
      id: '2',
      role: 'assistant',
      content: 'Hello user, how can I help?',
      timestamp: new Date().toISOString(),
    },
  ]

  it('renders all messages', () => {
    render(<ChatTranscript messages={mockMessages} />)
    expect(screen.getByText('Hello assistant')).toBeInTheDocument()
    expect(screen.getByText('Hello user, how can I help?')).toBeInTheDocument()
  })

  it('identifies the speakers correctly', () => {
    render(<ChatTranscript messages={mockMessages} />)
    expect(screen.getByText('You')).toBeInTheDocument()
    expect(screen.getByText('AI Assistant')).toBeInTheDocument()
  })
})
