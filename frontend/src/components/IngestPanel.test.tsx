import { render, screen, fireEvent } from '@testing-library/react'
import { IngestPanel } from './IngestPanel'
import { describe, it, expect, vi } from 'vitest'

describe('IngestPanel', () => {
  const defaultProps = {
    onIngest: vi.fn(),
    isBusy: false,
  }

  it('calls onIngest with undefined when "Use Default SOP" button is clicked', () => {
    const onIngest = vi.fn()
    render(<IngestPanel {...defaultProps} onIngest={onIngest} />)
    fireEvent.click(screen.getByRole('button', { name: /use default sop/i }))
    expect(onIngest).toHaveBeenCalledWith(undefined)
  })

  it('disables default ingest button when isBusy is true', () => {
    render(<IngestPanel {...defaultProps} isBusy={true} />)
    expect(screen.getByRole('button', { name: /ingesting/i })).toBeDisabled()
  })

  it('shows Ingesting text when isBusy', () => {
    render(<IngestPanel {...defaultProps} isBusy={true} />)
    expect(screen.getByText(/ingesting/i)).toBeInTheDocument()
  })

  it('shows "Use Default SOP" text when idle', () => {
    render(<IngestPanel {...defaultProps} isBusy={false} />)
    expect(screen.getByRole('button', { name: /use default sop/i })).toBeInTheDocument()
  })

  it('shows upload area when idle', () => {
    render(<IngestPanel {...defaultProps} isBusy={false} />)
    expect(screen.getByRole('button', { name: /choose .md or .txt file/i })).toBeInTheDocument()
  })

  it('shows selected filename after a file is chosen', () => {
    render(<IngestPanel {...defaultProps} />)
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    const file = new File(['content'], 'my-doc.md', { type: 'text/markdown' })
    fireEvent.change(input, { target: { files: [file] } })
    expect(screen.getByText('my-doc.md')).toBeInTheDocument()
  })

  it('shows "Upload & Ingest" button after a file is selected', () => {
    render(<IngestPanel {...defaultProps} />)
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    fireEvent.change(input, { target: { files: [new File(['x'], 'doc.md')] } })
    expect(screen.getByRole('button', { name: /upload & ingest/i })).toBeInTheDocument()
  })

  it('calls onIngest with the file when "Upload & Ingest" is clicked', () => {
    const onIngest = vi.fn()
    render(<IngestPanel {...defaultProps} onIngest={onIngest} />)
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    const file = new File(['content'], 'upload.md')
    fireEvent.change(input, { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: /upload & ingest/i }))
    expect(onIngest).toHaveBeenCalledWith(file)
  })

  it('clears the selected file when the remove button is clicked', () => {
    render(<IngestPanel {...defaultProps} />)
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    fireEvent.change(input, { target: { files: [new File(['x'], 'doc.md')] } })
    expect(screen.getByText('doc.md')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /remove selected file/i }))
    expect(screen.queryByText('doc.md')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /choose .md or .txt file/i })).toBeInTheDocument()
  })

  it('hides the choose-file button once a file is selected', () => {
    render(<IngestPanel {...defaultProps} />)
    const input = screen.getByLabelText(/select a .md or .txt document/i)
    fireEvent.change(input, { target: { files: [new File(['x'], 'doc.md')] } })
    expect(
      screen.queryByRole('button', { name: /choose .md or .txt file/i }),
    ).not.toBeInTheDocument()
  })
})
