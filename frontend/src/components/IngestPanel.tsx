import { Database, UploadCloud, Loader2 } from 'lucide-react'

interface IngestPanelProps {
  readonly onIngest: () => void
  readonly isBusy: boolean
}

export function IngestPanel({ onIngest, isBusy }: Readonly<IngestPanelProps>) {
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
      <button className="ingest-button" type="button" onClick={onIngest} disabled={isBusy}>
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
