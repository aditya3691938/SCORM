#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$DIR"

PORT="${1:-5050}"

echo "========================================================="
echo "   SCORM Implementation & Testing Studio Quickstart"
echo "========================================================="

PYTHON_BIN=""

if [ -x "$DIR/../LearningHubV3/venv/bin/python3" ]; then
    PYTHON_BIN="$DIR/../LearningHubV3/venv/bin/python3"
elif [ -x "$DIR/.venv/bin/python3" ]; then
    PYTHON_BIN="$DIR/.venv/bin/python3"
elif command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
fi

if [ -n "$PYTHON_BIN" ] && "$PYTHON_BIN" -c "import flask" 2>/dev/null; then
    echo "✔ Found Flask environment ($PYTHON_BIN)"
    echo "🚀 Starting Full Studio Server (Flask) on http://127.0.0.1:$PORT"
    exec "$PYTHON_BIN" app.py "$PORT"
else
    echo "ℹ Flask not found or not installed. Falling back to zero-dependency server."
    echo "🚀 Starting Zero-Dependency SCORM Server on http://127.0.0.1:$PORT"
    exec python3 server.py "$PORT"
fi
