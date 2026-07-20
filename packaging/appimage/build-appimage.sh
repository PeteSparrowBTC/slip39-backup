#!/usr/bin/env bash
# Builds SPS-SLIP39-x86_64.AppImage — the self-contained, offline, owner-side
# artifact: bundled Blazor WASM app + loopback-only Kestrel host. Run on Linux
# (CI ubuntu runner, or WSL for local builds).
#
# Usage:
#   ./build-appimage.sh <published-host-dir> <output.AppImage>
#
# <published-host-dir> must contain:
#   Slip39Demo.Host          (linux-x64 self-contained single-file publish)
#   wwwroot/                 (the published Blazor app)
# Produce it with:
#   dotnet publish Slip39Demo.Web  -c Release -o pub-web
#   dotnet publish Slip39Demo.Host -c Release -r linux-x64 -o pub-host
#   cp -r pub-web/wwwroot pub-host/
set -euo pipefail

HOST_DIR="${1:?usage: build-appimage.sh <published-host-dir> <output.AppImage>}"
OUTPUT="${2:?usage: build-appimage.sh <published-host-dir> <output.AppImage>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

[ -f "$HOST_DIR/Slip39Demo.Host" ] || { echo "error: $HOST_DIR/Slip39Demo.Host missing (linux-x64 publish?)"; exit 1; }
[ -d "$HOST_DIR/wwwroot" ]        || { echo "error: $HOST_DIR/wwwroot missing (copy the Web publish in)"; exit 1; }

# ── Assemble the AppDir ────────────────────────────────────────────────
APPDIR="$WORK/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/sps-slip39.desktop" "$APPDIR/"
# Icon: reuse the web app favicon (appimagetool requires an icon at the root).
cp "$HOST_DIR/wwwroot/favicon.png" "$APPDIR/sps-slip39.png"
cp "$HOST_DIR/Slip39Demo.Host" "$APPDIR/usr/bin/"
cp -r "$HOST_DIR/wwwroot" "$APPDIR/usr/bin/wwwroot"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/Slip39Demo.Host"

# ── Fetch appimagetool (pinned to the 'continuous' official build) ─────
TOOL="$WORK/appimagetool"
curl -fsSL -o "$TOOL" \
  "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
chmod +x "$TOOL"

# ── Build. --appimage-extract-and-run avoids needing FUSE (WSL/containers).
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$OUTPUT"
echo "built: $OUTPUT"
