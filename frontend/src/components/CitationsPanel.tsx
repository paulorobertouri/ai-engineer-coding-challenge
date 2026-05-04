import { BookOpen, FileText } from 'lucide-react'
import type { Citation } from '../types/chat'

interface CitationsPanelProps {
  citations: Citation[]
}

export function CitationsPanel({ citations }: CitationsPanelProps) {
  return (
    <section className="sidebar-card citations-panel" aria-labelledby="citations-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon" style={{ background: '#eff6ff' }}>
          <BookOpen size={15} color="#1d4ed8" />
        </div>
        <h2 id="citations-heading">Sources</h2>
        {citations.length > 0 && (
          <span className="citation-badge" aria-label={`${citations.length} citations`}>
            {citations.length}
          </span>
        )}
      </div>
      {citations.length === 0 ? (
        <p className="empty-state">Source citations will appear here after each AI response.</p>
      ) : (
        <ul className="citations-list">
          {citations.map((citation, index) => (
            <li key={`${citation.source}-${index}`} className="citation-item">
              <p className="citation-source">
                <FileText size={11} />
                {citation.source}
                {citation.startLine
                  ? ` · lines ${citation.startLine}–${citation.endLine ?? citation.startLine}`
                  : ''}
              </p>
              <p className="citation-snippet">{citation.snippet}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
