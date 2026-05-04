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
})
