import { ShieldAlert } from 'lucide-react'
import type { OperatorAuditDashboardResponse } from '../types/chat'

interface OperatorAuditPanelProps {
  dashboard: OperatorAuditDashboardResponse | null
  isLoading: boolean
  errorMessage: string | null
  feedbackFilter: '' | 'helpful' | 'unhelpful' | 'wrong-citation'
  onFeedbackFilterChange: (value: '' | 'helpful' | 'unhelpful' | 'wrong-citation') => void
  onRefresh: () => void
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

export function OperatorAuditPanel({
  dashboard,
  isLoading,
  errorMessage,
  feedbackFilter,
  onFeedbackFilterChange,
  onRefresh,
}: Readonly<OperatorAuditPanelProps>) {
  return (
    <section className="sidebar-card" aria-labelledby="operator-audit-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--danger">
          <ShieldAlert size={15} />
        </div>
        <h2 id="operator-audit-heading">Operator Audit</h2>
      </div>

      <div className="operator-audit-controls">
        <label htmlFor="operator-audit-feedback-filter">Feedback filter</label>
        <select
          id="operator-audit-feedback-filter"
          className="source-input"
          value={feedbackFilter}
          onChange={(event) =>
            onFeedbackFilterChange(
              event.target.value as '' | 'helpful' | 'unhelpful' | 'wrong-citation',
            )
          }
        >
          <option value="">All feedback</option>
          <option value="helpful">Helpful only</option>
          <option value="unhelpful">Unhelpful only</option>
          <option value="wrong-citation">Wrong citation only</option>
        </select>
        <button type="button" className="page-action-btn" onClick={onRefresh}>
          Refresh audit
        </button>
      </div>

      {isLoading && (
        <div className="empty-state" aria-live="polite" aria-busy="true">
          <span className="visually-hidden">Loading audit dashboard…</span>
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
        </div>
      )}
      {!isLoading && errorMessage && <p className="empty-state">{errorMessage}</p>}
      {!isLoading && !errorMessage && !dashboard && (
        <p className="empty-state">Audit data is not available yet.</p>
      )}

      {!isLoading && !errorMessage && dashboard && (
        <>
          <p className="source-viewer-meta">
            Feedback {dashboard.feedbackCount} · Low-confidence {dashboard.lowConfidenceSignalCount}{' '}
            · Failed ingests {dashboard.failedIngestCount}
          </p>
          <ul className="source-viewer-list">
            {dashboard.lowConfidenceSignals.slice(0, 5).map((entry, index) => (
              <li key={`signal-${entry.messageId}-${index}`} className="source-viewer-item">
                <div className="source-viewer-item-header">
                  <strong>{entry.feedbackType}</strong>
                  <span>{formatTimestamp(entry.timestampUtc)}</span>
                </div>
                <p className="source-viewer-meta">
                  {entry.conversationId} {entry.comment ? `· ${entry.comment}` : ''}
                </p>
              </li>
            ))}
            {dashboard.failedIngests.slice(0, 5).map((entry, index) => (
              <li key={`failed-${entry.sourceName}-${index}`} className="source-viewer-item">
                <div className="source-viewer-item-header">
                  <strong>{entry.sourceName || 'unknown source'}</strong>
                  <span>{formatTimestamp(entry.timestampUtc)}</span>
                </div>
                <p className="source-viewer-meta">
                  {entry.knowledgeBaseId || 'default'} · {entry.action} ·{' '}
                  {entry.safeSummary || 'failure'}
                </p>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
