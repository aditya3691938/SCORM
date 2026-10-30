#!/usr/bin/env bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$DIR"

PORT="${1:-5050}"

echo "========================================================="
echo "   SCORM Implementation & Testing Studio (.NET 8 / C#)"
echo "========================================================="

# Check common dotnet installation paths and configure environment
if [ -d "$HOME/.dotnet" ]; then
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

if ! command -v dotnet >/dev/null 2>&1; then
    for candidate in \
        "$HOME/.dotnet" \
        "/usr/local/share/dotnet" \
        "/opt/homebrew/bin" \
        "/usr/local/bin"; do
        if [ -x "$candidate/dotnet" ]; then
            export DOTNET_ROOT="${DOTNET_ROOT:-$candidate}"
            export PATH="$candidate:$PATH"
            break
        fi
    done
fi

# If dotnet is still not found, offer to install via official script
if ! command -v dotnet >/dev/null 2>&1; then
    echo "ℹ .NET SDK is not currently found on your system."
    echo "📥 Downloading and installing official .NET 8 SDK into ~/.dotnet (no sudo required)..."
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

if command -v dotnet >/dev/null 2>&1; then
    DOTNET_VER="$(dotnet --version 2>/dev/null || echo "detected")"
    echo "✔ Active .NET SDK: $DOTNET_VER"
    echo "🚀 Starting SCORM Testing Studio on http://127.0.0.1:$PORT"
    echo "Press Ctrl+C to stop."
    exec dotnet run --urls "http://127.0.0.1:$PORT"
else
    echo "❌ Error: Could not start dotnet. Please ensure .NET 8 SDK is installed."
    echo "Official installer: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi
