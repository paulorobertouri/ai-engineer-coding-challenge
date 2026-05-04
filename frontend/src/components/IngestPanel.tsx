import { Database, UploadCloud, Loader2 } from 'lucide-react'

interface IngestPanelProps {
  sourcePath: string
  onSourcePathChange: (value: string) => void
  onIngest: () => void
  isBusy: boolean
}

export function IngestPanel({
  sourcePath,
  onSourcePathChange,
  onIngest,
  isBusy,
}: IngestPanelProps) {
  return (
    <section className="sidebar-card" aria-labelledby="ingest-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon" style={{ background: '#f0fdf4' }}>
          <Database size={15} color="#166534" />
        </div>
        <h2 id="ingest-heading">Knowledge Base</h2>
      </div>
      <p className="description">
        Index the SOP document to enable grounded, citation-backed responses.
      </p>
      <label htmlFor="source-path">Document path</label>
      <input
        id="source-path"
        className="source-input"
        value={sourcePath}
        onChange={(event) => onSourcePathChange(event.target.value)}
        disabled={isBusy}
      />
      <button
        className="ingest-button"
        type="button"
        onClick={onIngest}
        disabled={isBusy || sourcePath.trim().length === 0}
      >
        {isBusy ? (
          <>
            <Loader2 size={14} className="animate-spin" />
            Ingesting...
          </>
        ) : (
          <>
            <UploadCloud size={14} />
            Run Ingest
          </>
        )}
      </button>
    </section>
  )
}
