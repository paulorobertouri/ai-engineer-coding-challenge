import { useState } from 'react'
import { BookOpen, FileText, ChevronDown, ChevronUp } from 'lucide-react'
import type { Citation, ConfidenceIndicator } from '../types/chat'

interface CitationsPanelProps {
  citations: Citation[]
  confidence?: ConfidenceIndicator | null
  hasMessages?: boolean
}

const confidenceLabels: Record<NonNullable<ConfidenceIndicator>['level'], string> = {
  high: 'High confidence',
  medium: 'Medium confidence',
  low: 'Low confidence',
  not_found: 'No evidence found',
}

export function CitationsPanel({
  citations,
  confidence,
  hasMessages = false,
}: CitationsPanelProps) {
  const [expanded, setExpanded] = useState<Set<number>>(new Set())
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null)

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

  function selectCitation(index: number) {
    setSelectedIndex(index)
    setExpanded((prev) => new Set(prev).add(index))
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
        {confidence && (
          <span className={`confidence-badge confidence-badge--${confidence.level}`}>
            {confidenceLabels[confidence.level]}
          </span>
        )}
        {citations.length > 0 && (
          <span className="citation-badge" aria-label={`${citations.length} citations`}>
            {citations.length}
          </span>
        )}
      </div>
      {confidence && (
        <p className="confidence-coverage">
          Evidence coverage: {Math.round(confidence.evidenceCoverage * 100)}%
        </p>
      )}
      {citations.length === 0 ? (
        <p className="empty-state">{emptyMessage}</p>
      ) : (
        <ul className="citations-list">
          {citations.map((citation, index) => {
            const isExpanded = expanded.has(index)
            const isSelected = selectedIndex === index
            const scoreLabel =
              typeof citation.score === 'number' ? `score ${citation.score.toFixed(3)}` : null
            return (
              <li
                key={`${citation.chunkId ?? citation.source}-${index}`}
                className={`citation-item${isSelected ? ' citation-item--selected' : ''}`}
              >
                <button
                  type="button"
                  className="citation-select-btn"
                  onClick={() => selectCitation(index)}
                  aria-pressed={isSelected}
                >
                  <p className="citation-source">
                    <FileText size={11} />
                    {citation.source}
                    {citation.startLine
                      ? ` · lines ${citation.startLine}–${citation.endLine ?? citation.startLine}`
                      : ''}
                  </p>
                  {(citation.sectionTitle || scoreLabel || citation.chunkId) && (
                    <p className="citation-source">
                      {citation.sectionTitle ? `Section: ${citation.sectionTitle}` : ''}
                      {citation.sectionTitle && scoreLabel ? ' · ' : ''}
                      {scoreLabel ?? ''}
                      {(citation.sectionTitle || scoreLabel) && citation.chunkId ? ' · ' : ''}
                      {citation.chunkId ? `Chunk: ${citation.chunkId}` : ''}
                    </p>
                  )}
                  {(citation.documentVersion || citation.knowledgeBaseId) && (
                    <p className="citation-source">
                      {citation.documentVersion ? `Version: ${citation.documentVersion}` : ''}
                      {citation.documentVersion && citation.knowledgeBaseId ? ' · ' : ''}
                      {citation.knowledgeBaseId ? `KB: ${citation.knowledgeBaseId}` : ''}
                    </p>
                  )}
                  <p
                    className={`citation-snippet${isExpanded ? ' citation-snippet--expanded' : ''}`}
                  >
                    {citation.snippet}
                  </p>
                </button>
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
