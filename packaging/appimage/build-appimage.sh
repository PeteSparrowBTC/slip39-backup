#!/usr/bin/env bash
# Builds SPS-SLIP39-x86_64.AppImage — the native-window (Photino/WebKitGTK)
# offline artifact for Tails 7+. Run on Linux (CI ubuntu runner, or WSL for
# local builds; WSL only needs bash + curl — dotnet publish can run on Windows).
#
# Usage:
#   ./build-appimage.sh <published-desktop-dir> <output.AppImage>
#
# <published-desktop-dir> must be a linux-x64 self-contained publish:
#   dotnet publish Slip39Demo.Desktop -c Release -r linux-x64 --self-contained -o pub-desktop
#
# System libraries (webkit2gtk-4.1, gtk3, libnotify) are NOT bundled — Tails 7
# ships them; that is a deliberate design constraint (Tails-only target).
set -euo pipefail

PUB_DIR="${1:?usage: build-appimage.sh <published-desktop-dir> <output.AppImage>}"
OUTPUT="${2:?usage: build-appimage.sh <published-desktop-dir> <output.AppImage>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

[ -f "$PUB_DIR/Slip39Demo.Desktop" ]  || { echo "error: $PUB_DIR/Slip39Demo.Desktop missing (linux-x64 publish?)"; exit 1; }
[ -f "$PUB_DIR/Photino.Native.so" ]   || { echo "error: Photino.Native.so missing from publish"; exit 1; }
[ -d "$PUB_DIR/wwwroot/_content/Slip39Demo.UI" ] || { echo "error: wwwroot/_content/Slip39Demo.UI missing (publish, not build?)"; exit 1; }

# Guard the Tails glibc floor: Tails 7 (Debian 13) ships glibc 2.41. If a
# Photino bump ever demands newer symbols, fail the build here instead of
# shipping an AppImage that dies on the user's stick with GLIBC_x not found.
MAX_GLIBC="$(objdump -T "$PUB_DIR/Photino.Native.so" | grep -o 'GLIBC_[0-9.]*' | sort -Vu | tail -1 | cut -d_ -f2)"
if [ "$(printf '%s\n' "$MAX_GLIBC" 2.41 | sort -V | tail -1)" != "2.41" ]; then
  echo "error: Photino.Native.so requires glibc $MAX_GLIBC > 2.41 (Tails 7)"; exit 1
fi

# ── Assemble the AppDir ────────────────────────────────────────────────
APPDIR="$WORK/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/sps-slip39.desktop" "$APPDIR/"
# Icon: reuse the app favicon (appimagetool requires an icon at the root).
cp "$PUB_DIR/wwwroot/_content/Slip39Demo.UI/favicon.png" "$APPDIR/sps-slip39.png"
cp -r "$PUB_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/Slip39Demo.Desktop"

# ── Fetch appimagetool (pinned to the 'continuous' official build) ─────
TOOL="$WORK/appimagetool"
curl -fsSL -o "$TOOL" \
  "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
chmod +x "$TOOL"

# ── Build. --appimage-extract-and-run avoids needing FUSE (WSL/containers).
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$OUTPUT"
echo "built: $OUTPUT"
