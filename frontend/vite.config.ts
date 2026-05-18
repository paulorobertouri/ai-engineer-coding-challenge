import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import type { UserConfig } from 'vite'
import type { InlineConfig } from 'vitest/node'

interface VitestConfigExport extends UserConfig {
  test?: InlineConfig
}

export default defineConfig({
  plugins: [react()],
  envDir: '../',
  server: {
    port: 5173,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov', 'html'],
      reportsDirectory: '../.build/coverage/frontend',
      include: [
        'src/pages/ChatPage.tsx',
        'src/components/chat/ChatComposer.tsx',
        'src/components/chat/CitationsPanel.tsx',
        'src/services/apiClient.ts',
      ],
      thresholds: {
        lines: 75,
        statements: 75,
        functions: 78,
        branches: 58,
      },
    },
  },
} as VitestConfigExport)
