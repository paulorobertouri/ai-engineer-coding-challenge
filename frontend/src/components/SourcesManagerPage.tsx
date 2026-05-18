import { Loader2, RefreshCcw, Trash2, FileText } from 'lucide-react'
import { IngestPanel } from './IngestPanel'
import type { SourceListItem } from '../types/chat'

interface SourcesManagerPageProps {
  readonly sources: SourceListItem[]
  readonly isLoadingSources: boolean
  readonly sourceBeingRemoved: string | null
  readonly isIngesting: boolean
  readonly onIngest: (file?: File) => void
  readonly onRefresh: () => void
  readonly onRemove: (source: string) => void
  readonly canOpenChat: boolean
  readonly onOpenChat: () => void
}

function formatIngestedAt(value?: string): string {
  if (!value) {
    return 'Unknown'
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return 'Unknown'
  }

  return parsed.toLocaleString()
}

export function SourcesManagerPage({
  sources,
  isLoadingSources,
  sourceBeingRemoved,
  isIngesting,
  onIngest,
  onRefresh,
  onRemove,
  canOpenChat,
  onOpenChat,
}: Readonly<SourcesManagerPageProps>) {
  return (
    <>
      <IngestPanel onIngest={onIngest} isBusy={isIngesting} />

      <section className="sidebar-card" aria-labelledby="sources-heading">
        <div className="sidebar-card-header">
          <div className="sidebar-card-icon" style={{ background: '#ecfeff' }}>
            <FileText size={15} color="#0f766e" />
          </div>
          <h2 id="sources-heading">Indexed Sources</h2>
        </div>

        <p className="description">List and remove ingested documents from the vector store.</p>

        <div className="page-action-row" style={{ marginTop: '0.5rem' }}>
          <button
            type="button"
            className="page-action-btn"
            onClick={onRefresh}
            disabled={isLoadingSources}
          >
            {isLoadingSources ? (
              <>
                <Loader2 size={14} className="animate-spin" />
                Refreshing...
              </>
            ) : (
              <>
                <RefreshCcw size={14} />
                Refresh sources
              </>
            )}
          </button>
          {canOpenChat && (
            <button type="button" className="page-action-btn" onClick={onOpenChat}>
              Open chat
            </button>
          )}
        </div>

        {sources.length === 0 ? (
          <p className="description" style={{ marginTop: '0.8rem' }}>
            No documents are currently indexed.
          </p>
        ) : (
          <ul className="sources-manager-list">
            {sources.map((source) => {
              const isRemoving = sourceBeingRemoved === source.source
              return (
                <li key={source.source} className="sources-manager-item">
                  <div>
                    <p className="sources-manager-name">{source.source}</p>
                    <p className="sources-manager-meta">
                      {source.chunkCount} chunk(s) · Last ingest:{' '}
                      {formatIngestedAt(source.ingestedAtUtc)}
                    </p>
                  </div>
                  <button
                    type="button"
                    className="page-action-btn"
                    onClick={() => onRemove(source.source)}
                    disabled={isRemoving}
                  >
                    {isRemoving ? (
                      <>
                        <Loader2 size={14} className="animate-spin" />
                        Removing...
                      </>
                    ) : (
                      <>
                        <Trash2 size={14} />
                        Remove
                      </>
                    )}
                  </button>
                </li>
              )
            })}
          </ul>
        )}
      </section>
    </>
  )
}
