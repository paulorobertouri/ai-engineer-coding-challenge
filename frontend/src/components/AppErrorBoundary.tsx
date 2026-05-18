import { ErrorBoundary, type FallbackProps } from 'react-error-boundary'

function ErrorFallback({ error, resetErrorBoundary }: FallbackProps) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center justify-center min-h-screen p-2 text-center bg-gray-50 dark:bg-gray-900"
    >
      <h2 className="text-2xl font-bold text-red-600 dark:text-red-400 mb-2">
        Something went wrong
      </h2>
      <pre className="text-sm bg-white dark:bg-gray-800 p-2 rounded border border-red-100 dark:border-red-900 mb-2 max-w-2xl overflow-auto text-gray-700 dark:text-gray-300">
        {error instanceof Error ? error.message : String(error)}
      </pre>
      <button
        onClick={resetErrorBoundary}
        className="px-2 py-2 bg-blue-600 hover:bg-blue-700 text-white font-medium rounded-lg transition-colors"
      >
        Try again
      </button>
    </div>
  )
}

export function AppErrorBoundary({ children }: { children: React.ReactNode }) {
  return (
    <ErrorBoundary FallbackComponent={ErrorFallback} onReset={() => window.location.reload()}>
      {children}
    </ErrorBoundary>
  )
}
