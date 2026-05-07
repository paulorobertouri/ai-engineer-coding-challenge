import { render, screen, fireEvent } from '@testing-library/react'
import { ChatComposer } from './ChatComposer'
import { describe, it, expect, vi } from 'vitest'

describe('ChatComposer', () => {
  it('calls onSubmit when the Send button is clicked', () => {
    const onSubmit = vi.fn()
    const onChange = vi.fn()
    render(<ChatComposer value="Hello" onChange={onChange} onSubmit={onSubmit} isBusy={false} />)

    const sendButton = screen.getByRole('button', { name: /Send/i })
    fireEvent.click(sendButton)

    expect(onSubmit).toHaveBeenCalled()
  })

  it('is disabled when isBusy is true', () => {
    render(<ChatComposer value="" onChange={() => {}} onSubmit={() => {}} isBusy={true} />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('calls onSubmit when Enter is pressed without Shift', () => {
    const onSubmit = vi.fn()
    render(<ChatComposer value="Hello" onChange={() => {}} onSubmit={onSubmit} isBusy={false} />)
    const textarea = screen.getByRole('textbox')
    fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: false })
    expect(onSubmit).toHaveBeenCalledOnce()
  })

  it('does not call onSubmit on Enter when value is empty', () => {
    const onSubmit = vi.fn()
    render(<ChatComposer value="" onChange={() => {}} onSubmit={onSubmit} isBusy={false} />)
    const textarea = screen.getByRole('textbox')
    fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: false })
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('does not call onSubmit on Enter when isBusy is true', () => {
    const onSubmit = vi.fn()
    render(<ChatComposer value="Hello" onChange={() => {}} onSubmit={onSubmit} isBusy={true} />)
    const textarea = screen.getByRole('textbox')
    fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: false })
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('does not call onSubmit on Shift+Enter', () => {
    const onSubmit = vi.fn()
    render(<ChatComposer value="Hello" onChange={() => {}} onSubmit={onSubmit} isBusy={false} />)
    const textarea = screen.getByRole('textbox')
    fireEvent.keyDown(textarea, { key: 'Enter', shiftKey: true })
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('calls onChange when textarea value changes', () => {
    const onChange = vi.fn()
    render(<ChatComposer value="" onChange={onChange} onSubmit={() => {}} isBusy={false} />)
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'new text' } })
    expect(onChange).toHaveBeenCalledWith('new text')
  })

  it('send button is disabled when value is whitespace only', () => {
    render(<ChatComposer value="   " onChange={() => {}} onSubmit={() => {}} isBusy={false} />)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('renders the composer hint text', () => {
    render(<ChatComposer value="" onChange={() => {}} onSubmit={() => {}} isBusy={false} />)
    expect(screen.getByText(/Enter to send/i)).toBeInTheDocument()
  })
})
