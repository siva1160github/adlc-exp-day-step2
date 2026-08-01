#!/bin/sh
set -eu

INDEX_HTML="/usr/share/nginx/html/index.html"

if [ -f "$INDEX_HTML" ]; then
  api_url="${VITE_API_URL:-}"
  # Escape / and & so sed replacement is safe.
  esc_api_url=$(printf '%s' "$api_url" | sed -e 's/[\/&]/\\&/g')
  sed -i "s|__VITE_API_URL__|$esc_api_url|g" "$INDEX_HTML"
fi

exec "$@"
