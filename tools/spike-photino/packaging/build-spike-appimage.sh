#!/usr/bin/env bash
# Builds the Phase-0 spike AppImage from a linux-x64 self-contained publish.
# Run in WSL/Linux (needs bash + curl only; dotnet publish happens on Windows).
#
# Usage:
#   ./build-spike-appimage.sh <published-dir> <output.AppImage>
#
# <published-dir> is produced with:
#   dotnet publish tools/spike-photino -c Release -r linux-x64 --self-contained -o pub-spike
set -euo pipefail

PUB_DIR="${1:?usage: build-spike-appimage.sh <published-dir> <output.AppImage>}"
OUTPUT="${2:?usage: build-spike-appimage.sh <published-dir> <output.AppImage>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

[ -f "$PUB_DIR/SpikePhotino" ]            || { echo "error: $PUB_DIR/SpikePhotino missing (linux-x64 publish?)"; exit 1; }
[ -f "$PUB_DIR/Photino.Native.so" ]       || { echo "error: Photino.Native.so missing from publish"; exit 1; }

# ── Assemble the AppDir ────────────────────────────────────────────────
APPDIR="$WORK/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/spike-photino.desktop" "$APPDIR/"
# Icon: reuse the web app favicon (appimagetool requires an icon at the root).
cp "$REPO_ROOT/Slip39Demo.Web/wwwroot/favicon.png" "$APPDIR/spike-photino.png"
cp -r "$PUB_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/SpikePhotino"

# ── Fetch appimagetool (pinned to the 'continuous' official build) ─────
TOOL="$WORK/appimagetool"
curl -fsSL -o "$TOOL" \
  "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
chmod +x "$TOOL"

# ── Build. --appimage-extract-and-run avoids needing FUSE (WSL/containers).
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$OUTPUT"
echo "built: $OUTPUT"
