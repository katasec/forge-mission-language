#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
AGENT_YAML="$REPO_ROOT/missions/concepts/debate/agent.yaml"
PORT=8080

# Prefer Homebrew Python over Xcode system Python
PYTHON="$(which python3.11 2>/dev/null || which python3.12 2>/dev/null || which python3)"

# Start forge serve in the background
forge serve "$AGENT_YAML" &
FORGE_PID=$!

# Ensure forge is killed when this script exits
trap "kill $FORGE_PID 2>/dev/null" EXIT

# Wait for the server to be ready
echo "Waiting for forge serve to be ready..."
until curl -sf "http://localhost:$PORT/v1/models" > /dev/null 2>&1; do
    sleep 0.5
done
echo "Ready."

# Ensure requests package is available
"$PYTHON" -c "import requests" 2>/dev/null || "$PYTHON" -m pip install --quiet --no-warn-script-location requests

# Run the Python client
"$PYTHON" "$(dirname "$0")/client.py"
