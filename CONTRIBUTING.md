# Contributing

## Prerequisites

- .NET SDK 10
- Node.js 22+
- Docker + Docker Compose (for full-stack and e2e flows)

## Quick Start

1. Run setup:
   - Bash: `./scripts/setup.sh`
   - PowerShell: `./scripts/setup.ps1`
2. Copy and adjust root environment:
   - `.env.example` -> `.env`
3. Run local checks before opening a PR.

## Branch And Commit Expectations

- Create topic branches from `main`.
- Keep commits focused and atomic (one task/scope per commit when possible).
- Use clear commit messages (examples: `feat: ...`, `fix: ...`, `ci: ...`, `docs: ...`).

## Required Local Checks

Run these before pushing:

1. Formatting/lint:
   - Bash: `./scripts/format.sh`
   - PowerShell: `./scripts/format.ps1`
2. Tests:
   - Bash: `./scripts/test.sh all`
   - PowerShell: `./scripts/test.ps1 all`
3. Build:
   - Backend: `dotnet build backend/AIEngineerCodingChallenge.Backend.slnx`
   - Frontend: `npm run build --prefix frontend`
4. Docker image validation (if Docker or container-related files changed):
   - `docker compose build backend frontend`

## Pull Request Checklist

- Scope and motivation are described.
- Tests were added/updated for behavior changes.
- Relevant docs were updated (`README.md`, backend/frontend docs, or docs/*).
- CI is green (backend, frontend, docker checks, security scans, e2e).

## Security And Dependency Hygiene

- Do not commit secrets in `.env` files.
- Keep dependencies current; Dependabot PRs are enabled.
- Follow the dependency policy in [docs/dependency-policy.md](docs/dependency-policy.md) before adding packages.
- New dependency PRs should state the package purpose, license, and why built-in platform features were not enough.
- Prefer permissive licenses and avoid paid, source-available-only, or strong-copyleft dependencies for default workflows.
- Verify no high-severity issues before merge:
  - `npm audit --audit-level=high` (frontend and e2e)
  - `dotnet list backend/AIEngineerCodingChallenge.Backend.slnx package --vulnerable --include-transitive`
