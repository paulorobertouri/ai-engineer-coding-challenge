import { render, screen } from '@testing-library/react'
import { StatusBanner } from './StatusBanner'
import { describe, it, expect } from 'vitest'

describe('StatusBanner', () => {
  it('renders the status message', () => {
    const status = { tone: 'info' as const, message: 'Test message' }
    render(<StatusBanner status={status} />)
    expect(screen.getByText('Test message')).toBeInTheDocument()
  })

  it('applies the correct tone attribute', () => {
    const status = { tone: 'error' as const, message: 'Error message' }
    render(<StatusBanner status={status} />)
    const element = screen.getByText('Error message')
    expect(element).toHaveAttribute('data-tone', 'error')
  })

  it('announces error statuses as alerts', () => {
    const status = { tone: 'error' as const, message: 'Error message' }
    render(<StatusBanner status={status} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Error message')
  })
})
