import { render, screen } from '@testing-library/react'
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
})
