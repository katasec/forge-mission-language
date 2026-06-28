#!/bin/bash
set -e

MISSION_DIR="$(cd "$(dirname "$0")" && pwd)"
FORGE=/Users/ameerdeen/.local/bin/forge

source ~/.bash_profile
unset ANTHROPIC_API_KEY

# Stop any existing containers
docker rm -f forge-what-is-next-webui 2>/dev/null || true

# Start forge serve locally in background
echo "Starting forge agent on http://localhost:8080 ..."
"$FORGE" serve "$MISSION_DIR/agent.yaml" &
FORGE_PID=$!
sleep 2

# Start Open WebUI pointing at local forge serve
echo "Starting Open WebUI on http://localhost:3000 ..."
docker run -d \
  --name forge-what-is-next-webui \
  -p 3000:8080 \
  -e OPENAI_API_KEY=forge \
  -e OPENAI_API_BASE_URL=http://host.docker.internal:8080/v1 \
  -v open-webui:/app/backend/data \
  ghcr.io/open-webui/open-webui:main

echo ""
echo "Open WebUI running at http://localhost:3000"
echo "Press Ctrl+C to stop the forge agent"

wait $FORGE_PID
