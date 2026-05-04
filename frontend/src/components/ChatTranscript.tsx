import { useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { User, Bot, Clock, MessageSquareDashed } from 'lucide-react'
import type { ChatMessage } from '../types/chat'
import { format } from 'date-fns'
import { MarkdownContent } from './MarkdownContent'
import { cn } from '../services/utils'

interface ChatTranscriptProps {
  messages: ChatMessage[]
}

export function ChatTranscript({ messages }: ChatTranscriptProps) {
  const endOfMessagesRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages])

  return (
    <section
      className="transcript flex-1 overflow-y-auto scrollbar-thin scrollbar-thumb-emerald-200 dark:scrollbar-thumb-slate-700"
      aria-label="Conversation history"
      aria-live="polite"
    >
      {messages.length === 0 && (
        <div className="transcript-empty select-none">
          <div className="transcript-empty__icon">
            <MessageSquareDashed size={22} className="text-emerald-700" />
          </div>
          <p>
            Ingest the SOP document, then ask anything about grocery store operating procedures.
          </p>
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
            </div>
          </motion.article>
        ))}
      </AnimatePresence>
      <div ref={endOfMessagesRef} />
    </section>
  )
}
