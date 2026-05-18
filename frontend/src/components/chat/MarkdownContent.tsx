import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { cn } from '../../services/utils'

interface MarkdownContentProps {
  content: string
  className?: string
}

function isAllowedHref(href: string): boolean {
  return /^(https?:|mailto:|tel:|\/|#)/i.test(href)
}

function isExternalHref(href: string): boolean {
  return /^https?:\/\//i.test(href)
}

/**
 * Senior-level Markdown renderer with accessibility and GFM support.
 */
export function MarkdownContent({ content, className }: MarkdownContentProps) {
  return (
    <div
      className={cn(
        'prose prose-sm max-w-none dark:prose-invert',
        'prose-p:my-2 prose-headings:scroll-mt-2 prose-headings:font-semibold',
        'prose-ul:my-2 prose-ol:my-2 prose-li:my-0.5',
        'prose-hr:border-slate-200 dark:prose-hr:border-slate-700',
        'prose-blockquote:border-emerald-200 prose-blockquote:bg-emerald-50/60 prose-blockquote:rounded-xl prose-blockquote:px-2 prose-blockquote:py-2',
        'prose-pre:bg-slate-950 prose-pre:text-slate-100 prose-pre:shadow-lg prose-pre:rounded-2xl',
        'prose-code:text-emerald-700 prose-code:before:content-none prose-code:after:content-none',
        className,
      )}
    >
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ href, children, ...props }) => {
            const safeHref = typeof href === 'string' && isAllowedHref(href) ? href : null

            if (!safeHref) {
              return <span className="text-slate-600">{children}</span>
            }

            const isExternal = isExternalHref(safeHref)

            return (
              <a
                {...props}
                href={safeHref}
                className="text-emerald-700 underline decoration-emerald-300 underline-offset-2 hover:decoration-emerald-500"
                target={isExternal ? '_blank' : undefined}
                rel={isExternal ? 'noopener noreferrer' : undefined}
              >
                {children}
                {isExternal && (
                  <span aria-hidden="true" className="ml-1 text-xs text-slate-500">
                    (external)
                  </span>
                )}
              </a>
            )
          },
          table: ({ ...props }) => (
            <div className="my-2 overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
              <table {...props} className="min-w-full border-collapse" />
            </div>
          ),
          th: ({ ...props }) => (
            <th
              {...props}
              className="border-b border-slate-200 bg-slate-50 px-2 py-2 font-semibold text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            />
          ),
          td: ({ ...props }) => (
            <td {...props} className="border-b border-slate-100 px-2 py-2 dark:border-slate-800" />
          ),
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}
