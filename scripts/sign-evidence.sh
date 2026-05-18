#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EVIDENCE_DIR="$ROOT/evidences/raw"
OUTPUT_FILE="$ROOT/evidences/evidence-signature-manifest.json"
SIGNING_KEY="${EVIDENCE_SIGNING_KEY:-}"

if [[ ! -d "$EVIDENCE_DIR" ]]; then
  echo "Evidence directory not found: $EVIDENCE_DIR" >&2
  exit 1
fi

files=()
while IFS= read -r -d '' file; do
  files+=("$file")
done < <(find "$EVIDENCE_DIR" -type f -print0 | sort -z)

printf '{\n  "generatedAtUtc": "%s",\n  "algorithm": "sha256",\n  "signing": "%s",\n  "artifacts": [\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$( [[ -n "$SIGNING_KEY" ]] && echo hmac-sha256 || echo unsigned )" > "$OUTPUT_FILE"

for i in "${!files[@]}"; do
  file="${files[$i]}"
  rel="${file#$ROOT/}"
  hash="$(sha256sum "$file" | awk '{print $1}')"

  signature=""
  if [[ -n "$SIGNING_KEY" ]]; then
    signature="$(printf '%s' "$hash" | openssl dgst -sha256 -hmac "$SIGNING_KEY" | awk '{print $2}')"
  fi

  printf '    {"path":"%s","sha256":"%s"' "$rel" "$hash" >> "$OUTPUT_FILE"
  if [[ -n "$signature" ]]; then
    printf ',"signature":"%s"' "$signature" >> "$OUTPUT_FILE"
  fi
  printf '}' >> "$OUTPUT_FILE"
  if [[ "$i" -lt $((${#files[@]} - 1)) ]]; then
    printf ',' >> "$OUTPUT_FILE"
  fi
  printf '\n' >> "$OUTPUT_FILE"
done

printf '  ]\n}\n' >> "$OUTPUT_FILE"

echo "Evidence manifest generated at $OUTPUT_FILE"
