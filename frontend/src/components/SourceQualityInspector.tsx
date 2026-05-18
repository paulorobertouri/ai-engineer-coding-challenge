import { Activity } from 'lucide-react'
import type { SourceQualityReportResponse } from '../types/chat'

interface SourceQualityInspectorProps {
  report: SourceQualityReportResponse | null
  isLoading: boolean
  errorMessage: string | null
}

function renderOutlierLabel(startLine?: number, endLine?: number): string {
  if (!startLine) {
    return 'lines n/a'
  }

  return `lines ${startLine}-${endLine ?? startLine}`
}

export function SourceQualityInspector({
  report,
  isLoading,
  errorMessage,
}: Readonly<SourceQualityInspectorProps>) {
  return (
    <section className="sidebar-card" aria-labelledby="source-quality-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--warning">
          <Activity size={15} />
        </div>
        <h2 id="source-quality-heading">Source Quality Inspector</h2>
      </div>

      {isLoading && (
        <div className="empty-state" aria-live="polite" aria-busy="true">
          <span className="visually-hidden">Evaluating source quality…</span>
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
        </div>
      )}
      {!isLoading && errorMessage && <p className="empty-state">{errorMessage}</p>}
      {!isLoading && !errorMessage && !report && (
        <p className="empty-state">Select a citation to inspect source quality metrics.</p>
      )}

      {!isLoading && !errorMessage && report && (
        <>
          <p className="source-viewer-meta">
            {report.source} · chunks {report.totalChunks}
          </p>
          <p className="source-viewer-meta">
            Duplicate sections: {report.duplicateSectionCount} · Weak extraction zones:{' '}
            {report.weakExtractionZoneCount}
          </p>

          <ul className="source-viewer-list">
            {report.shortestChunks.map((chunk) => (
              <li key={`short-${chunk.chunkId}`} className="source-viewer-item">
                <div className="source-viewer-item-header">
                  <strong>{chunk.sectionTitle || 'Untitled section'}</strong>
                  <span>short · {chunk.characterCount} chars</span>
                </div>
                <p className="source-viewer-meta">
                  {renderOutlierLabel(chunk.startLine, chunk.endLine)}
                </p>
              </li>
            ))}
            {report.longestChunks.map((chunk) => (
              <li key={`long-${chunk.chunkId}`} className="source-viewer-item">
                <div className="source-viewer-item-header">
                  <strong>{chunk.sectionTitle || 'Untitled section'}</strong>
                  <span>long · {chunk.characterCount} chars</span>
                </div>
                <p className="source-viewer-meta">
                  {renderOutlierLabel(chunk.startLine, chunk.endLine)}
                </p>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
