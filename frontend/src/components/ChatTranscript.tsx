import { useEffect, useRef, useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { User, Bot, Clock, MessageSquareDashed, Copy, Check } from 'lucide-react'
import type { ChatMessage } from '../types/chat'
import { format } from 'date-fns'
import { MarkdownContent } from './MarkdownContent'
import { TypingIndicator } from './TypingIndicator'
import { SuggestedPrompts } from './SuggestedPrompts'
import { cn } from '../services/utils'

interface ChatTranscriptProps {
  messages: ChatMessage[]
  isSending?: boolean
  onPromptSelect?: (prompt: string) => void
}

export function ChatTranscript({
  messages,
  isSending = false,
  onPromptSelect,
}: ChatTranscriptProps) {
  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)
  const [copiedMessageId, setCopiedMessageId] = useState<string | null>(null)

  async function copyAssistantMessage(messageId: string, content: string) {
    try {
      await navigator.clipboard.writeText(content)
      setCopiedMessageId(messageId)
      setTimeout(() => {
        setCopiedMessageId((current) => (current === messageId ? null : current))
      }, 1500)
    } catch {
      setCopiedMessageId(null)
    }
  }

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages, isSending])

  return (
    <section
      className="transcript flex-1 overflow-y-auto scrollbar-thin scrollbar-thumb-emerald-200 dark:scrollbar-thumb-slate-700"
      aria-label="Conversation history"
      aria-live="polite"
    >
      {messages.length === 0 && !isSending && (
        <div className="transcript-empty select-none">
          <div className="transcript-empty__icon">
            <MessageSquareDashed size={22} className="text-emerald-700" />
          </div>
          <p>Ask anything about the operating procedures.</p>
          {onPromptSelect && <SuggestedPrompts onSelect={onPromptSelect} />}
        </div>
      )}
      <AnimatePresence initial={false}>
        {messages.map((message) => (
          <motion.article
            key={message.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            data-role={message.role}
            className={cn(
              'message-card',
              message.role === 'user'
                ? 'message-card--user ml-auto flex-row-reverse'
                : 'message-card--assistant mr-auto',
            )}
          >
            <div className="message-avatar" aria-hidden>
              {message.role === 'assistant' ? <Bot size={15} /> : <User size={15} />}
            </div>

            <div className="message-stack">
              <div
                className={cn(
                  'message-meta',
                  message.role === 'user' ? 'message-meta--user' : 'message-meta--assistant',
                )}
              >
                <span>{message.role === 'assistant' ? 'AI Assistant' : 'You'}</span>
                <span className="message-time">
                  <Clock size={10} />
                  <span>{format(new Date(message.timestamp), 'HH:mm')}</span>
                </span>
              </div>

              <div
                className={cn('message-bubble', message.role === 'user' && 'message-bubble--user')}
              >
                <MarkdownContent content={message.content} className="message-markdown" />
              </div>

              {message.role === 'assistant' && (
                <button
                  type="button"
                  className="message-copy-btn"
                  onClick={() => copyAssistantMessage(message.id, message.content)}
                >
                  {copiedMessageId === message.id ? (
                    <>
                      <Check size={12} /> Copied
                    </>
                  ) : (
                    <>
                      <Copy size={12} /> Copy answer
                    </>
                  )}
                </button>
              )}
            </div>
          </motion.article>
        ))}
      </AnimatePresence>

      {isSending && (
        <motion.div
          initial={{ opacity: 0, y: 6 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          className="message-card message-card--assistant mr-auto"
        >
          <div className="message-avatar" aria-hidden>
            <Bot size={15} />
          </div>
          <div className="message-stack">
            <div className="message-meta message-meta--assistant">
              <span>AI Assistant</span>
            </div>
            <div className="message-bubble">
              <TypingIndicator />
            </div>
          </div>
        </motion.div>
      )}

      <div ref={endOfMessagesRef} />
    </section>
  )
}
