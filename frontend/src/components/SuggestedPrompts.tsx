const PROMPTS = [
  'What are the opening procedures for the manager on duty?',
  'What are the store hours on Monday?',
  'What should I do if a customer makes a complaint?',
  'What is the closing checklist?',
  'What is the policy on expired items?',
]

interface SuggestedPromptsProps {
  readonly onSelect: (prompt: string) => void
}

export function SuggestedPrompts({ onSelect }: Readonly<SuggestedPromptsProps>) {
  return (
    <div className="suggested-prompts">
      <p className="suggested-prompts__label">Try asking:</p>
      <div className="suggested-prompts__chips">
        {PROMPTS.map((prompt) => (
          <button
            key={prompt}
            className="suggested-prompt-chip"
            type="button"
            onClick={() => onSelect(prompt)}
          >
            {prompt}
          </button>
        ))}
      </div>
    </div>
  )
}
