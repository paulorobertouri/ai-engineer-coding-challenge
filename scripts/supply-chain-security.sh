#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPORT_DIR="$ROOT/security-reports"
TMP_DOTNET_TOOLS_DIR="$ROOT/.tmp-tools"
NODE_FORBIDDEN_LICENSES="GPL-1.0;GPL-2.0;GPL-3.0;AGPL-1.0;AGPL-3.0;LGPL-2.0;LGPL-2.1;LGPL-3.0;SSPL-1.0"

mkdir -p "$REPORT_DIR"
rm -f "$REPORT_DIR"/*.json "$REPORT_DIR"/*.spdx.json

echo "==> Secret scanning (Trivy filesystem scanner)..."
docker run --rm -v "$ROOT:/workspace" aquasec/trivy:0.65.0 \
  fs --scanners secret --severity HIGH,CRITICAL --exit-code 1 /workspace

echo "==> Building local images for SBOM and vulnerability scanning..."
docker build -t ai-sop-backend:local "$ROOT/backend/src/Api"
docker build -t ai-sop-frontend:local "$ROOT/frontend"

echo "==> Image vulnerability scanning (Trivy)..."
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.65.0 \
  image --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 ai-sop-backend:local

docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.65.0 \
  image --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 ai-sop-frontend:local

echo "==> Generating SBOMs (Syft)..."
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v "$REPORT_DIR:/out" anchore/syft:1.38.0 \
  ai-sop-backend:local -o spdx-json=/out/backend-image.spdx.json

docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v "$REPORT_DIR:/out" anchore/syft:1.38.0 \
  ai-sop-frontend:local -o spdx-json=/out/frontend-image.spdx.json

echo "==> Node dependency license checks..."
(
  cd "$ROOT/frontend"
  npx --yes license-checker@25.0.1 --production --failOn "$NODE_FORBIDDEN_LICENSES" --summary
)
(
  cd "$ROOT/e2e"
  npx --yes license-checker@25.0.1 --production --failOn "$NODE_FORBIDDEN_LICENSES" --summary
)

echo "==> .NET dependency license checks..."
mkdir -p "$TMP_DOTNET_TOOLS_DIR"
dotnet tool install dotnet-project-licenses --tool-path "$TMP_DOTNET_TOOLS_DIR" --version 2.7.1 >/dev/null 2>&1 || true
DOTNET_ROLL_FORWARD=Major "$TMP_DOTNET_TOOLS_DIR/dotnet-project-licenses" \
  -i "$ROOT/backend/AIEngineerCodingChallenge.Backend.slnx" \
  --forbidden-license-types "$ROOT/security/licenses/dotnet-forbidden-licenses.json" \
  --output jsonPretty \
  --outfile "$REPORT_DIR/dotnet-licenses.json"

echo "==> Supply chain checks completed. Reports saved in $REPORT_DIR"
