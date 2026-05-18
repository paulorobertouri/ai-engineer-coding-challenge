import { type FormEvent, type KeyboardEvent, useEffect, useRef } from 'react'
import { SendHorizonal } from 'lucide-react'
import type { ResponseLanguage } from '../types/chat'

interface ChatComposerProps {
  value: string
  onChange: (value: string) => void
  onSubmit: () => void
  isBusy: boolean
  responseLanguage: ResponseLanguage
  onResponseLanguageChange: (value: ResponseLanguage) => void
  responseTone: 'neutral' | 'formal' | 'friendly'
  onResponseToneChange: (value: 'neutral' | 'formal' | 'friendly') => void
  responseLength: 'short' | 'medium' | 'long'
  onResponseLengthChange: (value: 'short' | 'medium' | 'long') => void
  responseFormat: 'paragraph' | 'bullets' | 'checklist'
  onResponseFormatChange: (value: 'paragraph' | 'bullets' | 'checklist') => void
}

export function ChatComposer({
  value,
  onChange,
  onSubmit,
  isBusy,
  responseLanguage,
  onResponseLanguageChange,
  responseTone,
  onResponseToneChange,
  responseLength,
  onResponseLengthChange,
  responseFormat,
  onResponseFormatChange,
}: Readonly<ChatComposerProps>) {
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
      <label className="composer-input-label" htmlFor="chat-input">
        Ask about the grocery store SOP
      </label>
      <div className="composer-inner">
        <div className="composer-main-row">
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
          <button
            className="send-button"
            type="submit"
            disabled={!canSend}
            aria-label="Send message"
          >
            <SendHorizonal size={13} />
            <span>Send</span>
          </button>
        </div>

        <div className="composer-controls-grid">
          <label className="composer-language" htmlFor="response-language-select">
            <span>Answer language</span>
            <select
              id="response-language-select"
              value={responseLanguage}
              onChange={(event) =>
                onResponseLanguageChange(event.target.value as 'en' | 'es' | 'pt-BR' | 'fr')
              }
              disabled={isBusy}
              aria-label="Answer language"
            >
              <option value="en">English</option>
              <option value="es">Spanish</option>
              <option value="pt-BR">Portuguese (Brazil)</option>
              <option value="fr">French</option>
            </select>
          </label>
          <label className="composer-language" htmlFor="response-tone-select">
            <span>Tone</span>
            <select
              id="response-tone-select"
              value={responseTone}
              onChange={(event) =>
                onResponseToneChange(event.target.value as 'neutral' | 'formal' | 'friendly')
              }
              disabled={isBusy}
              aria-label="Answer tone"
            >
              <option value="neutral">Neutral</option>
              <option value="formal">Formal</option>
              <option value="friendly">Friendly</option>
            </select>
          </label>
          <label className="composer-language" htmlFor="response-length-select">
            <span>Length</span>
            <select
              id="response-length-select"
              value={responseLength}
              onChange={(event) =>
                onResponseLengthChange(event.target.value as 'short' | 'medium' | 'long')
              }
              disabled={isBusy}
              aria-label="Answer length"
            >
              <option value="short">Short</option>
              <option value="medium">Medium</option>
              <option value="long">Long</option>
            </select>
          </label>
          <label className="composer-language" htmlFor="response-format-select">
            <span>Format</span>
            <select
              id="response-format-select"
              value={responseFormat}
              onChange={(event) =>
                onResponseFormatChange(event.target.value as 'paragraph' | 'bullets' | 'checklist')
              }
              disabled={isBusy}
              aria-label="Answer format"
            >
              <option value="paragraph">Paragraph</option>
              <option value="bullets">Bullets</option>
              <option value="checklist">Checklist</option>
            </select>
          </label>
        </div>
      </div>
      <p className="composer-hint">Enter to send&ensp;·&ensp;Shift + Enter for new line</p>
    </form>
  )
}
