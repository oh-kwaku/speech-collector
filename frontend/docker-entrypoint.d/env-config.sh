#!/bin/sh
# Regenerates env-config.js from the container's real runtime environment
# every time it starts, so a Dokploy Environment Variable change just needs a
# restart — not a rebuild. Runs automatically: the official nginx image
# executes every executable *.sh in /docker-entrypoint.d/ before starting
# nginx.
set -eu

cat > /usr/share/nginx/html/env-config.js <<EOF
window.__RUNTIME_CONFIG__ = {
  VITE_API_BASE_URL: "${VITE_API_BASE_URL:-}"
};
EOF
