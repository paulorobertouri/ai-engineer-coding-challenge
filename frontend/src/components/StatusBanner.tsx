import { CheckCircle2, AlertTriangle, XCircle, Info } from 'lucide-react'
import type { StatusMessage } from '../types/chat'

const toneIcon: Record<StatusMessage['tone'], React.ReactNode> = {
  info: <Info size={14} aria-hidden />,
  success: <CheckCircle2 size={14} aria-hidden />,
  warning: <AlertTriangle size={14} aria-hidden />,
  error: <XCircle size={14} aria-hidden />,
}

interface StatusBannerProps {
  status: StatusMessage
}

export function StatusBanner({ status }: StatusBannerProps) {
  return (
    <section className="status-banner" data-tone={status.tone} aria-live="polite">
      {toneIcon[status.tone]}
      <span data-tone={status.tone}>{status.message}</span>
    </section>
  )
}
