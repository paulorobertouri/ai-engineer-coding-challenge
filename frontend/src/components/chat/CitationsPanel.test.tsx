import { render, screen, fireEvent } from '@testing-library/react'
import { CitationsPanel } from './CitationsPanel'
import { describe, it, expect, vi } from 'vitest'
import type { Citation } from '../../types/chat'

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

  it('renders section, score, and chunk metadata when present', () => {
    const citations: Citation[] = [
      {
        source: 'SOP.md',
        snippet: 'snippet',
        sectionTitle: 'Store Opening',
        score: 0.9182,
        chunkId: 'chunk-1',
        documentVersion: 'sha256:abc123def456',
        knowledgeBaseId: 'default',
      },
    ]
    render(<CitationsPanel citations={citations} />)
    expect(screen.getByText(/Section: Store Opening/)).toBeInTheDocument()
    expect(screen.getByText(/score 0.918/)).toBeInTheDocument()
    expect(screen.getByText(/Chunk: chunk-1/)).toBeInTheDocument()
    expect(screen.getByText(/Version: sha256:abc123def456/)).toBeInTheDocument()
    expect(screen.getByText(/KB: default/)).toBeInTheDocument()
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

  it('selects a citation when clicked', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'selectable text' }]
    render(<CitationsPanel citations={citations} />)

    const selectButton = screen.getByRole('button', { name: /SOP.md/i })
    fireEvent.click(selectButton)

    expect(selectButton).toHaveAttribute('aria-pressed', 'true')
  })

  it('calls onSelectCitation when a citation is selected', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'selectable text' }]
    const onSelectCitation = vi.fn()
    render(<CitationsPanel citations={citations} onSelectCitation={onSelectCitation} />)

    fireEvent.click(screen.getByRole('button', { name: /SOP.md/i }))

    expect(onSelectCitation).toHaveBeenCalledWith(citations[0])
  })

  it('supports keyboard selection for citation rows', () => {
    const citations: Citation[] = [{ source: 'SOP.md', snippet: 'keyboard text' }]
    render(<CitationsPanel citations={citations} />)

    const selectButton = screen.getByRole('button', { name: /SOP.md/i })
    selectButton.focus()
    fireEvent.keyDown(selectButton, { key: 'Enter', code: 'Enter' })
    fireEvent.click(selectButton)

    expect(selectButton).toHaveAttribute('aria-pressed', 'true')
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

  it('renders confidence badge and evidence coverage when provided', () => {
    render(
      <CitationsPanel
        citations={[]}
        confidence={{ level: 'medium', evidenceCoverage: 0.5 }}
        hasMessages={true}
      />,
    )

    expect(screen.getByText('Medium confidence')).toBeInTheDocument()
    expect(screen.getByText(/Evidence coverage: 50%/)).toBeInTheDocument()
  })

  it('renders not found confidence label', () => {
    render(
      <CitationsPanel
        citations={[]}
        confidence={{ level: 'not_found', evidenceCoverage: 0 }}
        hasMessages={true}
      />,
    )

    expect(screen.getByText('No evidence found')).toBeInTheDocument()
  })
})
