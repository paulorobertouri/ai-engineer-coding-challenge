import { GaugeCircle } from 'lucide-react'
import type { RetrievalBenchmarkDashboardResponse } from '../types/chat'

interface RetrievalBenchmarkPanelProps {
  dashboard: RetrievalBenchmarkDashboardResponse | null
  isLoading: boolean
  errorMessage: string | null
  onRun: () => void
}

function toPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

export function RetrievalBenchmarkPanel({
  dashboard,
  isLoading,
  errorMessage,
  onRun,
}: Readonly<RetrievalBenchmarkPanelProps>) {
  const latest = dashboard?.entries[0]

  return (
    <section className="sidebar-card" aria-labelledby="retrieval-benchmark-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--success-soft">
          <GaugeCircle size={15} />
        </div>
        <h2 id="retrieval-benchmark-heading">Retrieval Benchmarks</h2>
      </div>

      <button type="button" className="page-action-btn" onClick={onRun}>
        Run benchmark
      </button>

      {isLoading && (
        <div className="empty-state" aria-live="polite" aria-busy="true">
          <span className="visually-hidden">Loading retrieval metrics…</span>
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
        </div>
      )}
      {!isLoading && errorMessage && <p className="empty-state">{errorMessage}</p>}
      {!isLoading && !errorMessage && !latest && (
        <p className="empty-state">No benchmark runs yet.</p>
      )}

      {!isLoading && !errorMessage && latest && (
        <>
          <p className="source-viewer-meta">
            Latest commit: {latest.commit} · fixtures: {latest.fixtureCount}
          </p>
          <p className="source-viewer-meta">
            Precision {toPercent(latest.precision)} · Recall {toPercent(latest.recall)}
          </p>
        </>
      )}
    </section>
  )
}
