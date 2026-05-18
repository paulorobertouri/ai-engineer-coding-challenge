#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "==> Running backend tests with chaos profile enabled"

Chaos__Enabled=true \
Chaos__FailureRate=1.0 \
dotnet test "$ROOT/backend/src/Api.Tests/Api.Tests.csproj" --filter "FullyQualifiedName~ChaosInjectionMiddlewareTests|FullyQualifiedName~HealthControllerTests"

echo ""
echo "Chaos profile tests passed."
