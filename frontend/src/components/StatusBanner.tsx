import { CheckCircle2, AlertTriangle, XCircle, Info, X } from 'lucide-react'
import { forwardRef } from 'react'
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

export const StatusBanner = forwardRef<HTMLElement, StatusBannerProps>(function StatusBanner(
  { status, onDismiss },
  ref,
) {
  const role = status.tone === 'error' ? 'alert' : 'status'

  return (
    <section
      ref={ref}
      className="status-banner"
      data-tone={status.tone}
      aria-live={status.tone === 'error' ? 'assertive' : 'polite'}
      role={role}
      tabIndex={-1}
    >
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
})
