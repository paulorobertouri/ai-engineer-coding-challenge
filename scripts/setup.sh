#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> Setting up environment files..."

if [[ ! -f "$ROOT/.env" ]]; then
  cp "$ROOT/.env.example" "$ROOT/.env"
  echo "    Created $ROOT/.env from .env.example"
  echo "    !!! Open .env and fill in OpenAI__ApiKey before running the app."
else
  echo "    $ROOT/.env already exists, skipping."
fi

if [[ ! -f "$ROOT/frontend/.env" ]]; then
  cp "$ROOT/frontend/.env.example" "$ROOT/frontend/.env"
  echo "    Created $ROOT/frontend/.env from frontend/.env.example"
else
  echo "    $ROOT/frontend/.env already exists, skipping."
fi

echo ""
echo "==> Installing frontend dependencies..."
cd "$ROOT/frontend"
npm ci

echo ""
echo "==> Restoring backend (dotnet)..."
cd "$ROOT"
dotnet restore backend/AIEngineerCodingChallenge.Backend.slnx

echo ""
echo "Setup complete."
