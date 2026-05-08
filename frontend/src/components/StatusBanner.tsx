import { CheckCircle2, AlertTriangle, XCircle, Info, X } from 'lucide-react'
import type { StatusMessage } from '../types/chat'

const toneIcon: Record<StatusMessage['tone'], React.ReactNode> = {
  info: <Info size={14} aria-hidden />,
  success: <CheckCircle2 size={14} aria-hidden />,
  warning: <AlertTriangle size={14} aria-hidden />,
  error: <XCircle size={14} aria-hidden />,
}

interface StatusBannerProps {
  status: StatusMessage
  onDismiss?: () => void
}

export function StatusBanner({ status, onDismiss }: StatusBannerProps) {
  return (
    <section className="status-banner" data-tone={status.tone} aria-live="polite">
      {toneIcon[status.tone]}
      <span data-tone={status.tone}>{status.message}</span>
      {onDismiss && (
        <button
          className="status-banner__close"
          type="button"
          aria-label="Dismiss"
          onClick={onDismiss}
        >
          <X size={12} />
        </button>
      )}
    </section>
  )
}
