import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { ChatTranscript } from './ChatTranscript'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ChatMessage } from '../types/chat'

describe('ChatTranscript', () => {
  beforeEach(() => {
    Object.assign(globalThis.navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    })
  })

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
    expect(screen.getByText(/Ask anything about the operating procedures/i)).toBeInTheDocument()
  })

  it('does not show empty state when messages are present', () => {
    render(<ChatTranscript messages={mockMessages} />)
    expect(
      screen.queryByText(/Ask anything about the operating procedures/i),
    ).not.toBeInTheDocument()
  })

  it('shows a copy button for assistant messages', () => {
    render(<ChatTranscript messages={mockMessages} />)
    const button = screen.getByRole('button', { name: /copy answer/i })
    fireEvent.click(button)
    return waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('Hello user, how can I help?')
    })
  })
})
