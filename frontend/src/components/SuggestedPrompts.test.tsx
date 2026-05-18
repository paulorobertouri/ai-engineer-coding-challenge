import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { SuggestedPrompts } from './SuggestedPrompts'

describe('SuggestedPrompts', () => {
  it('renders default prompt chips', () => {
    render(<SuggestedPrompts onSelect={vi.fn()} />)

    expect(screen.getByText(/Try asking:/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /opening procedures/i })).toBeInTheDocument()
  })

  it('calls onSelect when a chip is clicked', () => {
    const onSelect = vi.fn()
    render(<SuggestedPrompts onSelect={onSelect} />)

    fireEvent.click(screen.getByRole('button', { name: /closing checklist/i }))

    expect(onSelect).toHaveBeenCalledWith('What is the closing checklist?')
  })

  it('renders custom prompts and label', () => {
    const onSelect = vi.fn()
    render(
      <SuggestedPrompts
        onSelect={onSelect}
        label="Suggested follow-up questions"
        prompts={['Follow-up one', 'Follow-up two']}
      />,
    )

    expect(screen.getByText(/Suggested follow-up questions/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Follow-up one' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Follow-up two' })).toBeInTheDocument()
  })
})
