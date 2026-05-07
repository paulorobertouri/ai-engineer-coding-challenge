import React from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { AppErrorBoundary } from './AppErrorBoundary'
import { describe, it, expect, vi } from 'vitest'

function ThrowingComponent({ shouldThrow }: { shouldThrow: boolean }) {
  if (shouldThrow) throw new Error('Test error message')
  return <div>OK content</div>
}

describe('AppErrorBoundary', () => {
  it('renders children when no error occurs', () => {
    render(
      <AppErrorBoundary>
        <ThrowingComponent shouldThrow={false} />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('OK content')).toBeInTheDocument()
  })

  it('shows error fallback heading when child throws', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    render(
      <AppErrorBoundary>
        <ThrowingComponent shouldThrow={true} />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('Something went wrong')).toBeInTheDocument()
    consoleError.mockRestore()
  })

  it('shows the error message in the fallback', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    render(
      <AppErrorBoundary>
        <ThrowingComponent shouldThrow={true} />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('Test error message')).toBeInTheDocument()
    consoleError.mockRestore()
  })

  it('shows the try again button in error state', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    render(
      <AppErrorBoundary>
        <ThrowingComponent shouldThrow={true} />
      </AppErrorBoundary>,
    )
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument()
    consoleError.mockRestore()
  })

  it('displays a stringified non-Error thrown value', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    const ThrowNonError = (): React.ReactNode => {
      throw 'plain string error'
    }

    render(
      <AppErrorBoundary>
        <ThrowNonError />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('plain string error')).toBeInTheDocument()
    consoleError.mockRestore()
  })

  it('calls window.location.reload when Try again is clicked', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    const reload = vi.fn()
    vi.stubGlobal('location', { ...window.location, reload })

    render(
      <AppErrorBoundary>
        <ThrowingComponent shouldThrow={true} />
      </AppErrorBoundary>,
    )

    fireEvent.click(screen.getByRole('button', { name: /try again/i }))
    expect(reload).toHaveBeenCalledOnce()

    vi.unstubAllGlobals()
    consoleError.mockRestore()
  })
})
