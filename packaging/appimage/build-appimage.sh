#!/usr/bin/env bash
# Builds slip39-backup-x86_64.AppImage — the native-window (Photino/WebKitGTK)
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
cp "$SCRIPT_DIR/slip39-backup.desktop" "$APPDIR/"
# Icon: reuse the app favicon (appimagetool requires an icon at the root).
cp "$PUB_DIR/wwwroot/_content/Slip39Demo.UI/favicon.png" "$APPDIR/slip39-backup.png"
cp -r "$PUB_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/Slip39Demo.Desktop"

# ── Bundle the official age binary ─────────────────────────────────────
# The app encrypts by running this program rather than the AgeSharp library
# linked into it: an encryption bug is invisible afterwards, so the side where
# mistakes cannot be detected gets the reference implementation.
#
# Pinned by version AND by checksum. An unpinned download would mean the bytes
# guarding somebody's seed phrase are whatever the network served that day.
# Update both together, deliberately, never one alone.
#
# The Linux build is statically linked with no interpreter and no glibc symbols,
# verified against v1.3.1, so it runs on Tails untouched and adds no library
# requirements to the AppImage.
AGE_VERSION="v1.3.1"
AGE_TARBALL="age--linux-amd64.tar.gz"
AGE_SHA256="bdc69c09cbdd6cf8b1f333d372a1f58247b3a33146406333e30c0f26e8f51377"

echo "Fetching age ..."
curl -fsSL -o "/"   "https://github.com/FiloSottile/age/releases/download//"

ACTUAL_SHA=""
if [ "" != "" ]; then
  echo "error: age tarball checksum mismatch"
  echo "  expected "
  echo "  actual   "
  exit 1
fi

# Lands at usr/bin/age/, which is AppContext.BaseDirectory + "age" at runtime,
# where NativeAgeEncryptor looks. age-plugin-batchpass must travel with it: age
# has no way to take a passphrase without a terminal otherwise.
tar -xzf "/" -C ""
mkdir -p "/usr/bin/age"
cp "/age/age" "/age/age-plugin-batchpass" "/usr/bin/age/"
chmod +x "/usr/bin/age/age" "/usr/bin/age/age-plugin-batchpass"

# Fail the build rather than ship an AppImage whose encryptor is absent: the app
# refuses to generate without it, so this would be a release nobody can use.
[ -x "/usr/bin/age/age" ] || { echo "error: bundled age binary missing or not executable"; exit 1; }
[ -x "/usr/bin/age/age-plugin-batchpass" ] || { echo "error: bundled batchpass plugin missing"; exit 1; }
echo "Bundled age: /usr/bin/bash: line 75: /usr/bin/age/age: No such file or directory"

# ── Fetch appimagetool (pinned to the 'continuous' official build) ─────
TOOL="$WORK/appimagetool"
curl -fsSL -o "$TOOL" \
  "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
chmod +x "$TOOL"

# ── Build. --appimage-extract-and-run avoids needing FUSE (WSL/containers).
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$OUTPUT"
echo "built: $OUTPUT"
