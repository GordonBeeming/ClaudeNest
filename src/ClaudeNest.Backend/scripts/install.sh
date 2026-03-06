#!/bin/bash
set -euo pipefail

# Backend URL and version are substituted by the server when this script is downloaded.
# The pairing token must be provided via the CLAUDENEST_TOKEN environment variable.
BACKEND_URL="%%BACKEND_URL%%"
VERSION="%%LATEST_VERSION%%"

if [ -z "${CLAUDENEST_TOKEN:-}" ]; then
  echo "Error: CLAUDENEST_TOKEN environment variable is required."
  echo "Usage: curl -sSL '${BACKEND_URL}/install.sh' | CLAUDENEST_TOKEN='<token>' bash"
  exit 1
fi

TOKEN="$CLAUDENEST_TOKEN"
REPO="GordonBeeming/ClaudeNest"
INSTALL_DIR="$HOME/.claudenest/bin"

echo "ClaudeNest Agent Installer"
echo "=========================="
echo ""

# Detect OS and architecture
OS=$(uname -s | tr '[:upper:]' '[:lower:]')
ARCH=$(uname -m)

case "$OS" in
  darwin) OS_RID="osx" ;;
  linux)  OS_RID="linux" ;;
  *)      echo "Error: Unsupported OS: $OS"; exit 1 ;;
esac

case "$ARCH" in
  x86_64|amd64) ARCH_RID="x64" ;;
  arm64|aarch64) ARCH_RID="arm64" ;;
  *)             echo "Error: Unsupported architecture: $ARCH"; exit 1 ;;
esac

RID="${OS_RID}-${ARCH_RID}"
BINARY_NAME="claudenest-agent-${RID}"
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/agent-v${VERSION}/${BINARY_NAME}"
VERSIONED_NAME="claudenest-agent-${VERSION}"
SYMLINK_NAME="claudenest-agent"

echo "Platform: ${RID}"
echo "Version:  ${VERSION}"
echo ""

# Create install directory
mkdir -p "$INSTALL_DIR"

# Download binary
echo "Downloading agent binary..."
if command -v curl &>/dev/null; then
  curl -fSL "$DOWNLOAD_URL" -o "${INSTALL_DIR}/${VERSIONED_NAME}"
elif command -v wget &>/dev/null; then
  wget -q "$DOWNLOAD_URL" -O "${INSTALL_DIR}/${VERSIONED_NAME}"
else
  echo "Error: Neither curl nor wget found. Please install one and retry."
  exit 1
fi

# Make executable
chmod +x "${INSTALL_DIR}/${VERSIONED_NAME}"

# Remove quarantine on macOS
if [ "$OS" = "darwin" ]; then
  xattr -d com.apple.quarantine "${INSTALL_DIR}/${VERSIONED_NAME}" 2>/dev/null || true
fi

# Create/update symlink
ln -sf "${VERSIONED_NAME}" "${INSTALL_DIR}/${SYMLINK_NAME}"

echo "Binary installed to ${INSTALL_DIR}/${VERSIONED_NAME}"
echo ""

# Run the install command
echo "Pairing agent with backend..."
"${INSTALL_DIR}/${VERSIONED_NAME}" install --token "$TOKEN" --backend "$BACKEND_URL" --path "$PWD"
