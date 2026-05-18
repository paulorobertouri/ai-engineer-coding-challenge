import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RetrievalBenchmarkPanel } from './RetrievalBenchmarkPanel'
import type { RetrievalBenchmarkDashboardResponse } from '../types/chat'

describe('RetrievalBenchmarkPanel', () => {
  it('renders loading, error and empty states', () => {
    const onRun = vi.fn()
    const { rerender } = render(
      <RetrievalBenchmarkPanel
        dashboard={null}
        isLoading={true}
        errorMessage={null}
        onRun={onRun}
      />,
    )
    expect(screen.getByText(/loading retrieval metrics/i)).toBeInTheDocument()

    rerender(
      <RetrievalBenchmarkPanel
        dashboard={null}
        isLoading={false}
        errorMessage={'Benchmark failed'}
        onRun={onRun}
      />,
    )
    expect(screen.getByText(/benchmark failed/i)).toBeInTheDocument()

    rerender(
      <RetrievalBenchmarkPanel
        dashboard={{ generatedAtUtc: '2026-01-01T00:00:00Z', entries: [] }}
        isLoading={false}
        errorMessage={null}
        onRun={onRun}
      />,
    )
    expect(screen.getByText(/no benchmark runs yet/i)).toBeInTheDocument()
  })

  it('renders latest benchmark metrics and run action', () => {
    const onRun = vi.fn()
    const dashboard: RetrievalBenchmarkDashboardResponse = {
      generatedAtUtc: '2026-01-01T00:00:00Z',
      entries: [
        {
          runId: 'run-1',
          timestampUtc: '2026-01-01T00:00:00Z',
          commit: 'abc1234',
          fixtureCount: 20,
          precision: 0.83,
          recall: 0.71,
        },
      ],
    }

    render(
      <RetrievalBenchmarkPanel
        dashboard={dashboard}
        isLoading={false}
        errorMessage={null}
        onRun={onRun}
      />,
    )

    expect(screen.getByText(/latest commit: abc1234/i)).toBeInTheDocument()
    expect(screen.getByText(/fixtures: 20/i)).toBeInTheDocument()
    expect(screen.getByText(/precision 83%/i)).toBeInTheDocument()
    expect(screen.getByText(/recall 71%/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /run benchmark/i }))
    expect(onRun).toHaveBeenCalledTimes(1)
  })
})
