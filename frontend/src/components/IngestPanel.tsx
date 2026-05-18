import { useRef, useState } from 'react'
import { Database, UploadCloud, Loader2, FileText, X } from 'lucide-react'

interface IngestPanelProps {
  readonly onIngest: (file?: File) => void
  readonly isBusy: boolean
}

export function IngestPanel({ onIngest, isBusy }: Readonly<IngestPanelProps>) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null
    setSelectedFile(file)
  }

  function handleClearFile() {
    setSelectedFile(null)
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  function handleDefaultIngest() {
    onIngest(undefined)
  }

  function handleUploadIngest() {
    if (selectedFile) onIngest(selectedFile)
  }

  return (
    <section className="sidebar-card" aria-labelledby="ingest-heading">
      <div className="sidebar-card-header">
        <div className="sidebar-card-icon sidebar-card-icon--success">
          <Database size={15} />
        </div>
        <h2 id="ingest-heading">Knowledge Base</h2>
      </div>

      {/* Default SOP */}
      <p className="description">
        Index the built-in Grocery Store SOP to enable grounded, citation-backed responses.
      </p>
      <button
        className="ingest-button"
        type="button"
        onClick={handleDefaultIngest}
        disabled={isBusy}
      >
        {isBusy && !selectedFile ? (
          <>
            <Loader2 size={14} className="animate-spin" />
            Ingesting…
          </>
        ) : (
          <>
            <UploadCloud size={14} />
            Use Default SOP
          </>
        )}
      </button>

      {/* Divider */}
      <div className="ingest-divider">
        <span>or upload your own</span>
      </div>

      {/* File upload */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".md,.txt"
        className="ingest-file-input"
        aria-label="Select a .md or .txt document"
        onChange={handleFileChange}
        disabled={isBusy}
      />

      {selectedFile ? (
        <div className="ingest-file-selected">
          <FileText size={13} />
          <span className="ingest-file-name" title={selectedFile.name}>
            {selectedFile.name}
          </span>
          <button
            className="ingest-file-clear"
            type="button"
            aria-label="Remove selected file"
            onClick={handleClearFile}
            disabled={isBusy}
          >
            <X size={12} />
          </button>
        </div>
      ) : (
        <button
          className="ingest-upload-area"
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={isBusy}
        >
          <FileText size={16} />
          <span>Choose .md or .txt file</span>
        </button>
      )}

      {selectedFile && (
        <button
          className="ingest-button"
          type="button"
          onClick={handleUploadIngest}
          disabled={isBusy}
          data-spacing-top="sm"
        >
          {isBusy ? (
            <>
              <Loader2 size={14} className="animate-spin" />
              Ingesting…
            </>
          ) : (
            <>
              <UploadCloud size={14} />
              Upload &amp; Ingest
            </>
          )}
        </button>
      )}
    </section>
  )
}
