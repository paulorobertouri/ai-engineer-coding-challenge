#!/bin/sh
set -eu

API_BASE_URL="${BACKEND_API_URL:-${VITE_API_BASE_URL:-http://localhost:5181}}"

cat > /usr/share/nginx/html/config.json <<EOF
{
  "apiBaseUrl": "${API_BASE_URL}"
}
EOF
