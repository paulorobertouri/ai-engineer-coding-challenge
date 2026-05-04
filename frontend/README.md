# Frontend

React 19 + TypeScript + Vite chat UI for the Grocery Store SOP Assistant.

## Features

- Chat transcript with streaming-style message display
- Composer with loading and disabled states
- Ingest panel to trigger SOP ingestion from the backend
- Citations panel displaying source chunks returned by the API
- Zod-validated API client (`services/apiClient.ts`)
- Status banner for health, ingest, and error feedback

## Local Development

```bash
cd frontend
npm run dev
```

The app calls `http://localhost:5181` by default.  
Override via `VITE_API_BASE_URL` in `frontend/.env` (created automatically by `scripts/setup.sh`).

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
