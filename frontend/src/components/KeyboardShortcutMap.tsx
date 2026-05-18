interface KeyboardShortcutMapProps {
  onClose: () => void
}

const SHORTCUTS: Array<{ keys: string; action: string }> = [
  { keys: '?', action: 'Open keyboard shortcut map' },
  { keys: 'Esc', action: 'Close keyboard shortcut map' },
  { keys: '/', action: 'Focus chat input' },
  { keys: 'Alt + N', action: 'Start a new conversation' },
  { keys: 'Alt + E', action: 'Export current conversation' },
  { keys: 'Enter', action: 'Send message (inside chat input)' },
  { keys: 'Shift + Enter', action: 'Insert newline (inside chat input)' },
]

export function KeyboardShortcutMap({ onClose }: Readonly<KeyboardShortcutMapProps>) {
  return (
    <div className="shortcut-map-backdrop" role="presentation" onClick={onClose}>
      <section
        className="shortcut-map"
        role="dialog"
        aria-modal="true"
        aria-label="Keyboard shortcut map"
        onClick={(event) => event.stopPropagation()}
      >
        <header className="shortcut-map__header">
          <h2>Keyboard shortcuts</h2>
          <button
            type="button"
            className="shortcut-map__close"
            onClick={onClose}
            aria-label="Close shortcut map"
          >
            Close
          </button>
        </header>

        <ul className="shortcut-map__list">
          {SHORTCUTS.map((shortcut) => (
            <li key={shortcut.keys} className="shortcut-map__row">
              <kbd>{shortcut.keys}</kbd>
              <span>{shortcut.action}</span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
