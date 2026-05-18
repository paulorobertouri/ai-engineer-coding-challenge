import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { OperatorAuditPanel } from './OperatorAuditPanel'
import type { OperatorAuditDashboardResponse } from '../types/chat'

describe('OperatorAuditPanel', () => {
  it('renders loading, error and empty states', () => {
    const onFeedbackFilterChange = vi.fn()
    const onRefresh = vi.fn()

    const { rerender } = render(
      <OperatorAuditPanel
        dashboard={null}
        isLoading={true}
        errorMessage={null}
        feedbackFilter={''}
        onFeedbackFilterChange={onFeedbackFilterChange}
        onRefresh={onRefresh}
      />,
    )
    expect(screen.getByText(/loading audit dashboard/i)).toBeInTheDocument()

    rerender(
      <OperatorAuditPanel
        dashboard={null}
        isLoading={false}
        errorMessage={'Audit unavailable'}
        feedbackFilter={''}
        onFeedbackFilterChange={onFeedbackFilterChange}
        onRefresh={onRefresh}
      />,
    )
    expect(screen.getByText(/audit unavailable/i)).toBeInTheDocument()

    rerender(
      <OperatorAuditPanel
        dashboard={null}
        isLoading={false}
        errorMessage={null}
        feedbackFilter={''}
        onFeedbackFilterChange={onFeedbackFilterChange}
        onRefresh={onRefresh}
      />,
    )
    expect(screen.getByText(/audit data is not available yet/i)).toBeInTheDocument()
  })

  it('renders dashboard data and triggers callbacks', () => {
    const onFeedbackFilterChange = vi.fn()
    const onRefresh = vi.fn()

    const dashboard: OperatorAuditDashboardResponse = {
      generatedAtUtc: '2026-01-01T00:00:00Z',
      fromUtc: '2025-12-31T00:00:00Z',
      toUtc: '2026-01-01T00:00:00Z',
      feedbackCount: 8,
      lowConfidenceSignalCount: 3,
      failedIngestCount: 1,
      feedback: [],
      lowConfidenceSignals: [
        {
          timestampUtc: '2026-01-01T00:00:00Z',
          type: 'feedback',
          severity: 'warning',
          conversationId: 'conv-1',
          messageId: 'msg-1',
          feedbackType: 'unhelpful',
          comment: 'Missing details',
          action: 'investigate',
          outcome: 'open',
          knowledgeBaseId: 'default',
          sourceName: 'SOP.md',
          safeSummary: 'Low confidence answer',
        },
      ],
      failedIngests: [
        {
          timestampUtc: '2026-01-01T00:00:00Z',
          type: 'ingest',
          severity: 'error',
          conversationId: 'conv-2',
          messageId: 'msg-2',
          feedbackType: 'wrong-citation',
          action: 'retry',
          outcome: 'failed',
          knowledgeBaseId: 'kb-ops',
          sourceName: 'Ops.md',
          safeSummary: 'Vector write failed',
        },
      ],
    }

    render(
      <OperatorAuditPanel
        dashboard={dashboard}
        isLoading={false}
        errorMessage={null}
        feedbackFilter={''}
        onFeedbackFilterChange={onFeedbackFilterChange}
        onRefresh={onRefresh}
      />,
    )

    expect(screen.getByText(/feedback 8/i)).toBeInTheDocument()
    expect(screen.getByText(/low-confidence 3/i)).toBeInTheDocument()
    expect(screen.getByText(/failed ingests 1/i)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/feedback filter/i), {
      target: { value: 'helpful' },
    })
    expect(onFeedbackFilterChange).toHaveBeenCalledWith('helpful')

    fireEvent.click(screen.getByRole('button', { name: /refresh audit/i }))
    expect(onRefresh).toHaveBeenCalledTimes(1)
  })
})
