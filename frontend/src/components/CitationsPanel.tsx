import { useState } from 'react'
import { BookOpen, FileText, ChevronDown, ChevronUp } from 'lucide-react'
import type { Citation } from '../types/chat'

interface CitationsPanelProps {
  citations: Citation[]
  hasMessages?: boolean
}

export function CitationsPanel({ citations, hasMessages = false }: CitationsPanelProps) {
  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  function toggleExpand(index: number) {
    setExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(index)) {
        next.delete(index)
      } else {
        next.add(index)
      }
      return next
    })
  }

  const emptyMessage = hasMessages
    ? 'No sources were cited for the last response.'
    : 'Source citations will appear here after each AI response.'

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
        <p className="empty-state">{emptyMessage}</p>
      ) : (
        <ul className="citations-list">
          {citations.map((citation, index) => {
            const isExpanded = expanded.has(index)
            return (
              <li key={`${citation.source}-${index}`} className="citation-item">
                <p className="citation-source">
                  <FileText size={11} />
                  {citation.source}
                  {citation.startLine
                    ? ` · lines ${citation.startLine}–${citation.endLine ?? citation.startLine}`
                    : ''}
                </p>
                <p className={`citation-snippet${isExpanded ? ' citation-snippet--expanded' : ''}`}>
                  {citation.snippet}
                </p>
                <button
                  className="citation-expand-btn"
                  type="button"
                  onClick={() => toggleExpand(index)}
                  aria-expanded={isExpanded}
                >
                  {isExpanded ? (
                    <>
                      <ChevronUp size={11} /> Show less
                    </>
                  ) : (
                    <>
                      <ChevronDown size={11} /> Show more
                    </>
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
