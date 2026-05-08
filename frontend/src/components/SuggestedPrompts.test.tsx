import { render, screen, fireEvent } from '@testing-library/react'
import { SuggestedPrompts } from './SuggestedPrompts'
import { describe, it, expect, vi } from 'vitest'

describe('SuggestedPrompts', () => {
  it('renders all prompt chips', () => {
    render(<SuggestedPrompts onSelect={vi.fn()} />)
    expect(screen.getAllByRole('button').length).toBeGreaterThan(0)
  })

  it('calls onSelect with the prompt text when a chip is clicked', () => {
    const onSelect = vi.fn()
    render(<SuggestedPrompts onSelect={onSelect} />)
    const buttons = screen.getAllByRole('button')
    fireEvent.click(buttons[0])
    expect(onSelect).toHaveBeenCalledOnce()
    expect(onSelect).toHaveBeenCalledWith(expect.any(String))
  })

  it('calls onSelect with the correct prompt for each chip', () => {
    const onSelect = vi.fn()
    render(<SuggestedPrompts onSelect={onSelect} />)
    const buttons = screen.getAllByRole('button')
    buttons.forEach((btn, i) => {
      fireEvent.click(btn)
      expect(onSelect).toHaveBeenNthCalledWith(i + 1, btn.textContent)
    })
  })

  it('renders the "Try asking:" label', () => {
    render(<SuggestedPrompts onSelect={vi.fn()} />)
    expect(screen.getByText(/try asking/i)).toBeInTheDocument()
  })
})
