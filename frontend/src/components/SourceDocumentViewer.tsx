import { useEffect, useMemo, useRef } from 'react'
import { FileSearch } from 'lucide-react'
import { MarkdownContent } from './chat/MarkdownContent'
import type {
  Citation,
  SourceComparisonResponse,
  SourceDocumentChunk,
  SourceDocumentResponse,
} from '../types/chat'

interface SourceDocumentViewerProps {
  document: SourceDocumentResponse | null
  comparison: SourceComparisonResponse | null
  activeCitation: Citation | null
  isLoading: boolean
  errorMessage: string | null
}

function findSelectedChunk(
  chunks: SourceDocumentChunk[],
  activeCitation: Citation | null,
): string | null {
  if (!activeCitation) {
    return null
  }

  if (activeCitation.chunkId) {
    return activeCitation.chunkId
  }

  if (!activeCitation.startLine) {
    return null
  }

  const citationStartLine = activeCitation.startLine
  const citationEndLine = activeCitation.endLine ?? citationStartLine

  return (
    chunks.find((chunk) => {
      if (!chunk.startLine) {
        return false
      }

      const chunkEndLine = chunk.endLine ?? chunk.startLine

      return chunk.startLine <= citationEndLine && chunkEndLine >= citationStartLine
    })?.chunkId ?? null
  )
}

export function SourceDocumentViewer({
  document,
  comparison,
  activeCitation,
  isLoading,
  errorMessage,
}: Readonly<SourceDocumentViewerProps>) {
  const selectedChunkId = useMemo(
    () => (document ? findSelectedChunk(document.chunks, activeCitation) : null),
    [activeCitation, document],
  )

  const chunkRefs = useRef<Map<string, HTMLLIElement>>(new Map())

  useEffect(() => {
    if (!selectedChunkId) {
      return
    }

    const target = chunkRefs.current.get(selectedChunkId)
    if (target) {
      target.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    }
  }, [selectedChunkId])

  return (
    <section className="sidebar-card source-viewer-panel" aria-labelledby="source-viewer-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--indigo">
          <FileSearch size={15} />
        </div>
        <h2 id="source-viewer-heading">Source Viewer</h2>
      </div>

      {isLoading && (
        <div className="empty-state" aria-live="polite" aria-busy="true">
          <span className="visually-hidden">Loading source document…</span>
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
          <div className="skeleton skeleton-line" />
        </div>
      )}
      {!isLoading && errorMessage && <p className="empty-state">{errorMessage}</p>}
      {!isLoading && !errorMessage && !document && (
        <p className="empty-state">Select a citation to inspect its source content.</p>
      )}

      {!isLoading && !errorMessage && document && (
        <>
          <p className="source-viewer-meta">
            {document.source}
            {document.documentVersion ? ` · ${document.documentVersion}` : ''}
          </p>
          <ul className="source-viewer-list">
            {document.chunks.map((chunk) => {
              const isSelected = chunk.chunkId === selectedChunkId
              const lineLabel =
                chunk.startLine === undefined
                  ? 'Lines unavailable'
                  : `Lines ${chunk.startLine}-${chunk.endLine ?? chunk.startLine}`

              return (
                <li
                  key={chunk.chunkId}
                  ref={(element) => {
                    if (element) {
                      chunkRefs.current.set(chunk.chunkId, element)
                    } else {
                      chunkRefs.current.delete(chunk.chunkId)
                    }
                  }}
                  className={`source-viewer-item${isSelected ? ' source-viewer-item--selected' : ''}`}
                  aria-current={isSelected ? 'true' : undefined}
                >
                  <div className="source-viewer-item-header">
                    <strong>{chunk.sectionTitle || 'Untitled section'}</strong>
                    <span>{lineLabel}</span>
                  </div>
                  <MarkdownContent content={chunk.content} className="source-viewer-markdown" />
                </li>
              )
            })}
          </ul>

          {comparison && comparison.chunks.length > 0 && (
            <div className="source-comparison-panel" aria-label="Policy comparison view">
              <p className="source-viewer-meta">
                Policy comparison: {comparison.ingestedDocumentVersion ?? 'ingested'} vs{' '}
                {comparison.currentDocumentVersion}
              </p>
              <p className="source-viewer-meta">
                Changed chunks: {comparison.changedChunkCount} / {comparison.totalComparedChunks}
              </p>
              <ul className="source-viewer-list">
                {comparison.chunks.map((chunk) => (
                  <li
                    key={`${chunk.index ?? 'na'}-${chunk.ingestedChunkId ?? chunk.currentChunkId ?? 'unknown'}`}
                    className={`source-viewer-item${chunk.isImpactedCitation ? ' source-viewer-item--selected' : ''}`}
                  >
                    <div className="source-viewer-item-header">
                      <strong>{chunk.sectionTitle || 'Untitled section'}</strong>
                      <span>{chunk.changeType}</span>
                    </div>
                    <div className="source-comparison-columns">
                      <div>
                        <h3>Ingested</h3>
                        <MarkdownContent
                          content={chunk.ingestedContent || '_No ingested content_'}
                          className="source-viewer-markdown"
                        />
                      </div>
                      <div>
                        <h3>Current</h3>
                        <MarkdownContent
                          content={chunk.currentContent || '_No current content_'}
                          className="source-viewer-markdown"
                        />
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}
    </section>
  )
}
