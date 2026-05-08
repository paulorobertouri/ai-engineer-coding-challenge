import { ShoppingCart } from 'lucide-react'

interface AppHeaderProps {
  readonly badge?: string
}

export function AppHeader({ badge = 'GPT-4o-mini' }: Readonly<AppHeaderProps>) {
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
        <span className="app-header-badge">{badge}</span>
      </div>
    </header>
  )
}
