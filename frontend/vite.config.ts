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
      reportsDirectory: './coverage',
      include: [
        'src/pages/ChatPage.tsx',
        'src/components/ChatComposer.tsx',
        'src/components/CitationsPanel.tsx',
        'src/services/apiClient.ts',
      ],
      thresholds: {
        lines: 80,
        statements: 80,
        functions: 80,
        branches: 70,
      },
    },
  },
} as VitestConfigExport)
