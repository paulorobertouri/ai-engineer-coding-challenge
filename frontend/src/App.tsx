import { ChatPage } from './pages/ChatPage'
import './App.css'
import { AppErrorBoundary } from './components/AppErrorBoundary'

function App() {
  return (
    <AppErrorBoundary>
      <ChatPage />
    </AppErrorBoundary>
  )
}

export default App
