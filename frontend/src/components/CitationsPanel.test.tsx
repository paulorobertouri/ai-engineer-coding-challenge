import { render, screen, fireEvent } from '@testing-library/react'
import { CitationsPanel } from './CitationsPanel'
import { describe, it, expect } from 'vitest'
import type { Citation } from '../types/chat'

describe('CitationsPanel', () => {
  it('shows empty state when no citations', () => {
    render(<CitationsPanel citations={[]} />)
    expect(screen.getByText(/citations will appear/i)).toBeInTheDocument()
  })

  it('renders citation source and snippet', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'Store policy details' }]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getByText('SOP.md')).toBeInTheDocument()
    expect(screen.getByText('Store policy details')).toBeInTheDocument()
  })

  it('shows the citation count badge', () => {
    const citations: Citation[] = [
      { source: 'SOP.md', snippet: 'snippet 1' },
      { source: 'SOP.md', snippet: 'snippet 2' },
    ]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getByLabelText('2 citations')).toBeInTheDocument()
  })

  it('renders line range when startLine and endLine are provided', () => {
    const citations: Citation[] = [
      { source: 'SOP.md', snippet: 'snippet', startLine: 5, endLine: 10 },
    ]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getByText(/lines 5–10/)).toBeInTheDocument()
  })

  it('renders startLine only when endLine is absent', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'snippet', startLine: 7 }]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getByText(/lines 7–7/)).toBeInTheDocument()
  })

  it('does not show empty state when citations are present', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'text' }]
    render(<CitationsPanel citations={citations} />)
    expect(screen.queryByText(/citations will appear/i)).not.toBeInTheDocument()
  })

  it('shows "Show more" button for each citation', () => {
    const citations: Citation[] = [
      { source: 'SOP.md', snippet: 'text one' },
      { source: 'SOP.md', snippet: 'text two' },
    ]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getAllByRole('button', { name: /show more/i })).toHaveLength(2)
  })

  it('toggles citation expansion when "Show more" is clicked', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'expandable text' }]
    render(<CitationsPanel citations={citations} />)

    const btn = screen.getByRole('button', { name: /show more/i })
    expect(btn).toHaveAttribute('aria-expanded', 'false')

    fireEvent.click(btn)
    expect(screen.getByRole('button', { name: /show less/i })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
  })

  it('collapses citation when "Show less" is clicked', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'expandable text' }]
    render(<CitationsPanel citations={citations} />)

    fireEvent.click(screen.getByRole('button', { name: /show more/i }))
    fireEvent.click(screen.getByRole('button', { name: /show less/i }))
    expect(screen.getByRole('button', { name: /show more/i })).toHaveAttribute(
      'aria-expanded',
      'false',
    )
  })

  it('shows "No sources were cited" when hasMessages is true and citations are empty', () => {
    render(<CitationsPanel citations={[]} hasMessages={true} />)
    expect(screen.getByText(/No sources were cited/i)).toBeInTheDocument()
  })

  it('shows generic empty message when hasMessages is false and citations are empty', () => {
    render(<CitationsPanel citations={[]} hasMessages={false} />)
    expect(screen.getByText(/citations will appear/i)).toBeInTheDocument()
  })
})
