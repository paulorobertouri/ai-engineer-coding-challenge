import { type FormEvent, type KeyboardEvent, useEffect, useRef } from 'react'
import { SendHorizonal } from 'lucide-react'

interface ChatComposerProps {
  value: string
  onChange: (value: string) => void
  onSubmit: () => void
  isBusy: boolean
}

export function ChatComposer({ value, onChange, onSubmit, isBusy }: ChatComposerProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    const el = textareaRef.current
    if (el) {
      el.style.height = 'auto'
      el.style.height = `${Math.min(el.scrollHeight, 112)}px`
    }
  }, [value])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      if (!isBusy && value.trim().length > 0) {
        onSubmit()
      }
    }
  }

  const canSend = !isBusy && value.trim().length > 0

  return (
    <form className="composer" onSubmit={handleSubmit}>
      <label htmlFor="chat-input">Ask about the grocery store SOP</label>
      <div className="composer-inner">
        <textarea
          ref={textareaRef}
          id="chat-input"
          rows={1}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Example: What are the opening checklist steps for the manager on duty?"
          disabled={isBusy}
        />
        <button className="send-button" type="submit" disabled={!canSend} aria-label="Send message">
          <SendHorizonal size={13} />
          <span>Send</span>
        </button>
      </div>
      <p className="composer-hint">Enter to send&ensp;·&ensp;Shift + Enter for new line</p>
    </form>
  )
}
