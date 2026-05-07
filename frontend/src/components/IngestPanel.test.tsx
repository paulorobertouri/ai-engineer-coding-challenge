import { render, screen, fireEvent } from '@testing-library/react'
import { IngestPanel } from './IngestPanel'
import { describe, it, expect, vi } from 'vitest'

describe('IngestPanel', () => {
  const defaultProps = {
    onIngest: vi.fn(),
    isBusy: false,
  }

  it('calls onIngest when Run Ingest button is clicked', () => {
    const onIngest = vi.fn()
    render(<IngestPanel {...defaultProps} onIngest={onIngest} />)
    fireEvent.click(screen.getByRole('button'))
    expect(onIngest).toHaveBeenCalledOnce()
  })

  it('disables button when isBusy is true', () => {
    render(<IngestPanel {...defaultProps} isBusy={true} />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('shows Ingesting text when isBusy', () => {
    render(<IngestPanel {...defaultProps} isBusy={true} />)
    expect(screen.getByText(/ingesting/i)).toBeInTheDocument()
  })

  it('shows Run Ingest text when idle', () => {
    render(<IngestPanel {...defaultProps} isBusy={false} />)
    expect(screen.getByText(/run ingest/i)).toBeInTheDocument()
  })
})
