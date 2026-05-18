import { useState } from 'react'
import { BookOpen, FileText, ChevronDown, ChevronUp } from 'lucide-react'
import type { Citation, ConfidenceIndicator } from '../types/chat'

interface CitationsPanelProps {
  citations: Citation[]
  confidence?: ConfidenceIndicator | null
  hasMessages?: boolean
  onSelectCitation?: (citation: Citation) => void
}

interface CitationListItemProps {
  citation: Citation
  index: number
  isExpanded: boolean
  isSelected: boolean
  onSelect: (index: number) => void
  onToggleExpand: (index: number) => void
}

function formatLineRange(citation: Citation): string {
  if (!citation.startLine) {
    return ''
  }

  return ` · lines ${citation.startLine}–${citation.endLine ?? citation.startLine}`
}

function buildSectionScoreChunkText(citation: Citation): string | null {
  const parts: string[] = []
  if (citation.sectionTitle) {
    parts.push(`Section: ${citation.sectionTitle}`)
  }

  if (typeof citation.score === 'number') {
    parts.push(`score ${citation.score.toFixed(3)}`)
  }

  if (citation.chunkId) {
    parts.push(`Chunk: ${citation.chunkId}`)
  }

  return parts.length > 0 ? parts.join(' · ') : null
}

function buildVersionText(citation: Citation): string | null {
  const parts: string[] = []

  if (citation.documentVersion) {
    parts.push(`Version: ${citation.documentVersion}`)
  }

  if (citation.knowledgeBaseId) {
    parts.push(`KB: ${citation.knowledgeBaseId}`)
  }

  return parts.length > 0 ? parts.join(' · ') : null
}

const confidenceLabels: Record<NonNullable<ConfidenceIndicator>['level'], string> = {
  high: 'High confidence',
  medium: 'Medium confidence',
  low: 'Low confidence',
  not_found: 'No evidence found',
}

function CitationListItem({
  citation,
  index,
  isExpanded,
  isSelected,
  onSelect,
  onToggleExpand,
}: Readonly<CitationListItemProps>) {
  const sectionScoreChunkText = buildSectionScoreChunkText(citation)
  const versionText = buildVersionText(citation)

  return (
    <li className={`citation-item${isSelected ? ' citation-item--selected' : ''}`}>
      <button
        type="button"
        className="citation-select-btn"
        onClick={() => onSelect(index)}
        aria-pressed={isSelected}
      >
        <p className="citation-source">
          <FileText size={11} />
          {citation.source}
          {formatLineRange(citation)}
        </p>
        {sectionScoreChunkText && <p className="citation-source">{sectionScoreChunkText}</p>}
        {versionText && <p className="citation-source">{versionText}</p>}
        <p className={`citation-snippet${isExpanded ? ' citation-snippet--expanded' : ''}`}>
          {citation.snippet}
        </p>
      </button>
      <button
        className="citation-expand-btn"
        type="button"
        onClick={() => onToggleExpand(index)}
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
}

export function CitationsPanel({
  citations,
  confidence,
  hasMessages = false,
  onSelectCitation,
}: Readonly<CitationsPanelProps>) {
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
    onSelectCitation?.(citations[index])
  }

  const emptyMessage = hasMessages
    ? 'No sources were cited for the last response.'
    : 'Source citations will appear here after each AI response.'

  return (
    <section className="sidebar-card citations-panel" aria-labelledby="citations-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--info">
          <BookOpen size={15} />
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
          {citations.map((citation, index) => (
            <CitationListItem
              key={`${citation.chunkId ?? citation.source}-${index}`}
              citation={citation}
              index={index}
              isExpanded={expanded.has(index)}
              isSelected={selectedIndex === index}
              onSelect={selectCitation}
              onToggleExpand={toggleExpand}
            />
          ))}
        </ul>
      )}
    </section>
  )
}
