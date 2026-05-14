import { ShoppingCart } from 'lucide-react'

interface AppHeaderProps {
  readonly badge?: string
  readonly badgeClassName?: string
  readonly onNewChat?: () => void
}

export function AppHeader({
  badge = 'GPT-4o-mini',
  badgeClassName,
  onNewChat,
}: Readonly<AppHeaderProps>) {
  return (
    <header className="app-header">
      <div className="app-header-inner">
        <div className="app-header-icon">
          <ShoppingCart size={15} color="white" strokeWidth={2.5} />
        </div>
        <div className="app-header-text">
          <h1>SOP Assistant</h1>
          <p>Grocery Store Operating Procedures · Powered by AI</p>
        </div>
        <div className="app-header-actions">
          {onNewChat && (
            <button type="button" className="app-header-action-btn" onClick={onNewChat}>
              New chat
            </button>
          )}
          <span className={`app-header-badge${badgeClassName ? ` ${badgeClassName}` : ''}`}>
            {badge}
          </span>
        </div>
      </div>
    </header>
  )
}
