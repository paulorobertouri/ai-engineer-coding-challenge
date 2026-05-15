import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { ChatTranscript } from './ChatTranscript'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import type { ChatMessage } from '../types/chat'

describe('ChatTranscript', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    })

    Object.assign(globalThis.navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
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

  it('emits selected feedback for assistant messages', () => {
    const onFeedbackSubmit = vi.fn()
    render(<ChatTranscript messages={mockMessages} onFeedbackSubmit={onFeedbackSubmit} />)

    fireEvent.click(screen.getByRole('button', { name: /^helpful$/i }))
    fireEvent.click(screen.getByRole('button', { name: /^unhelpful$/i }))
    fireEvent.click(screen.getByRole('button', { name: /wrong citation/i }))

    expect(onFeedbackSubmit).toHaveBeenNthCalledWith(1, '2', 'helpful')
    expect(onFeedbackSubmit).toHaveBeenNthCalledWith(2, '2', 'unhelpful')
    expect(onFeedbackSubmit).toHaveBeenNthCalledWith(3, '2', 'wrong-citation')
  })

  it('uses non-animated scrolling when reduced motion is preferred', () => {
    window.matchMedia = vi.fn().mockImplementation((query: string) => ({
      matches: query.includes('prefers-reduced-motion'),
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))

    const scrollIntoView = vi
      .spyOn(HTMLElement.prototype, 'scrollIntoView')
      .mockImplementation(() => {})

    render(<ChatTranscript messages={mockMessages} />)

    expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'auto', block: 'end' })
  })
})
