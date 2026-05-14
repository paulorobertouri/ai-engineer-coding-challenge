# Frontend

React 19 + TypeScript + Vite chat UI for the Grocery Store SOP Assistant.

When containerized, static assets are served by nginx using `frontend/nginx/default.conf`, which provides SPA fallback routing and modern response security headers.

## Components

| Component          | Description                                                                                           |
| ------------------ | ----------------------------------------------------------------------------------------------------- |
| `ChatPage`         | Root page — owns conversation state, health check on mount, ingest and send handlers (`useCallback`)  |
| `ChatTranscript`   | Animated message history (Framer Motion fade + slide); auto-scrolls to latest turn and supports copy-answer action |
| `ChatComposer`     | Auto-expanding textarea; Enter to send, Shift+Enter for newline; disabled while sending               |
| `CitationsPanel`   | Sidebar card listing source chunks returned by the API (`source`, `snippet`, optional line range)     |
| `IngestPanel`      | Sidebar card with a single "Run Ingest" button; source path is server-side only                       |
| `StatusBanner`     | Colour-coded banner (`info` / `success` / `warning` / `error`) for health, ingest, and error feedback |
| `MarkdownContent`  | Renders assistant messages as Markdown                                                                |
| `AppErrorBoundary` | React error boundary catching unhandled render errors                                                 |

## Services & Types

| File                    | Description                                                                                                                                                 |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `services/apiClient.ts` | Typed HTTP client for `GET /api/v1/health`, `POST /api/v1/ingest`, `POST /api/v1/chat`; base URL from runtime `/config.json` with `VITE_API_BASE_URL` fallback |
| `services/utils.ts`     | Shared utility helpers                                                                                                                                      |
| `types/chat.ts`         | `ChatMessage`, `Citation`, `ChatRequest`, `ChatResponse`, `IngestRequest`, `IngestResponse`, `HealthResponse`, `StatusMessage`                              |
| `types/validation.ts`   | Zod schemas (`ChatRequestSchema`, `IngestRequestSchema`) validating outgoing payloads                                                                       |

UX quality-of-life features:
- Retry last failed chat request and retry failed ingest attempts
- Start a fresh conversation with `New chat`
- Visible offline/fallback mode badge when backend reports fallback mode
- Session persistence for current conversation and citations via `sessionStorage`

## Local Development

```bash
cd frontend
npm run dev
```

The app calls `http://localhost:5181` by default. In local development, override via `VITE_API_BASE_URL` in `frontend/.env` (created automatically by `scripts/setup.sh`).

In Docker/nginx deployments, the image now reads backend URL at container startup from `BACKEND_API_URL` and writes `/config.json`. This allows one built image to run against different backend environments.

## Available Scripts

| Command          | Description                          |
| ---------------- | ------------------------------------ |
| `npm run dev`    | Start Vite dev server                |
| `npm run build`  | Type-check and bundle for production |
| `npm test`       | Run unit tests with Vitest           |
| `npm run lint`   | ESLint check                         |
| `npm run format` | Prettier auto-format                 |

Or use the repo-level scripts from the root:

```bash
./scripts/test.sh frontend
./scripts/format.sh [--fix]
```

PowerShell on Windows:

```powershell
.\scripts\test.ps1 frontend
.\scripts\format.ps1 [--fix]
```

## Unit Tests

| File                        | Coverage                                                                        |
| --------------------------- | ------------------------------------------------------------------------------- |
| `AppErrorBoundary.test.tsx` | Renders children normally; displays fallback UI on render error                 |
| `ChatComposer.test.tsx`     | Renders, sends on Enter, inserts newline on Shift+Enter, disables while sending |
| `ChatPage.test.tsx`         | Integration tests for health check on mount, ingest flow, and send flow         |
| `ChatTranscript.test.tsx`   | Renders empty state, displays messages, shows correct role labels               |
| `CitationsPanel.test.tsx`   | Renders empty state and a list of citations with source and snippet             |
| `IngestPanel.test.tsx`      | Renders button, triggers `onIngest`, disables while busy                        |
| `MarkdownContent.test.tsx`  | Renders plain text and basic Markdown (bold, lists)                             |
| `StatusBanner.test.tsx`     | Renders all tone variants with correct accessible roles                         |
| `validation.test.ts`        | Validates `ChatRequestSchema` and `IngestRequestSchema` with valid/invalid data |
