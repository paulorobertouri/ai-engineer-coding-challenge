import { render, screen, fireEvent } from '@testing-library/react'
import { IngestPanel } from './IngestPanel'
import { describe, it, expect, vi } from 'vitest'

describe('IngestPanel', () => {
  const defaultProps = {
    sourcePath: '/knowledge-base/SOP.md',
    onSourcePathChange: vi.fn(),
    onIngest: vi.fn(),
    isBusy: false,
  }

  it('renders the source path input with current value', () => {
    render(<IngestPanel {...defaultProps} />)
    expect(screen.getByRole('textbox')).toHaveValue('/knowledge-base/SOP.md')
  })

  it('calls onIngest when Run Ingest button is clicked', () => {
    const onIngest = vi.fn()
    render(<IngestPanel {...defaultProps} onIngest={onIngest} />)
    fireEvent.click(screen.getByRole('button'))
    expect(onIngest).toHaveBeenCalledOnce()
  })

  it('disables button and input when isBusy is true', () => {
    render(<IngestPanel {...defaultProps} isBusy={true} />)
    expect(screen.getByRole('button')).toBeDisabled()
    expect(screen.getByRole('textbox')).toBeDisabled()
  })

  it('disables button when sourcePath is empty', () => {
    render(<IngestPanel {...defaultProps} sourcePath="" />)
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

  it('calls onSourcePathChange when input changes', () => {
    const onSourcePathChange = vi.fn()
    render(<IngestPanel {...defaultProps} onSourcePathChange={onSourcePathChange} />)
    fireEvent.change(screen.getByRole('textbox'), { target: { value: '/new/path.md' } })
    expect(onSourcePathChange).toHaveBeenCalledWith('/new/path.md')
  })
})
