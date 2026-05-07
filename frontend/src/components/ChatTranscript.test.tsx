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

  it('shows empty state when there are no messages', () => {
    render(<ChatTranscript messages={[]} />)
    expect(screen.getByText(/Ingest the SOP document/i)).toBeInTheDocument()
  })

  it('does not show empty state when messages are present', () => {
    render(<ChatTranscript messages={mockMessages} />)
    expect(screen.queryByText(/Ingest the SOP document/i)).not.toBeInTheDocument()
  })
})
