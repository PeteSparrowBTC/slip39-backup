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
AGE_TARBALL="age-${AGE_VERSION}-linux-amd64.tar.gz"
AGE_SHA256="bdc69c09cbdd6cf8b1f333d372a1f58247b3a33146406333e30c0f26e8f51377"
AGE_URL="https://github.com/FiloSottile/age/releases/download/${AGE_VERSION}/${AGE_TARBALL}"

echo "Fetching age ${AGE_VERSION}..."
curl -fsSL -o "$WORK/$AGE_TARBALL" "$AGE_URL"

# Compared explicitly rather than through `sha256sum -c`, so a failure prints
# both values. A mismatch means the bytes that would encrypt somebody's seed
# phrase are not the bytes that were reviewed.
ACTUAL_SHA="$(sha256sum "$WORK/$AGE_TARBALL" | cut -d' ' -f1)"
if [ "$ACTUAL_SHA" != "$AGE_SHA256" ]; then
  echo "error: age tarball checksum mismatch"
  echo "  expected $AGE_SHA256"
  echo "  actual   $ACTUAL_SHA"
  exit 1
fi
echo "Verified age tarball against its pinned checksum."

# Lands at usr/bin/age/, which is AppContext.BaseDirectory + "age" at runtime,
# where NativeAgeEncryptor looks. age-plugin-batchpass must travel with it: age
# has no way to take a passphrase without a terminal otherwise.
tar -xzf "$WORK/$AGE_TARBALL" -C "$WORK"
mkdir -p "$APPDIR/usr/bin/age"
cp "$WORK/age/age" "$WORK/age/age-plugin-batchpass" "$APPDIR/usr/bin/age/"
chmod +x "$APPDIR/usr/bin/age/age" "$APPDIR/usr/bin/age/age-plugin-batchpass"

# Fail the build rather than ship an AppImage whose encryptor is absent: the app
# refuses to generate without it, so this would be a release nobody can use.
[ -x "$APPDIR/usr/bin/age/age" ] || { echo "error: bundled age binary missing or not executable"; exit 1; }
[ -x "$APPDIR/usr/bin/age/age-plugin-batchpass" ] || { echo "error: bundled batchpass plugin missing"; exit 1; }

# Run it rather than only checking the file is present. A binary for the wrong
# architecture, or one that cannot start, would otherwise reach a user's USB
# stick and fail on the airgapped machine where nobody can fix it.
BUNDLED_AGE_VERSION="$("$APPDIR/usr/bin/age/age" --version)"
echo "Bundled age reports: $BUNDLED_AGE_VERSION"
[ "$BUNDLED_AGE_VERSION" = "$AGE_VERSION" ] || {
  echo "error: bundled age reports '$BUNDLED_AGE_VERSION', expected '$AGE_VERSION'"; exit 1; }

# ── Fetch appimagetool, pinned by version AND by checksum ──────────────
# 'continuous' was not a pin. It is a tag the project rebuilds in place (last on
# 2025-12-04), so it names whatever was pushed most recently rather than
# anything anyone reviewed, and the comment claiming it was pinned was the same
# kind of claim as the checksum comparison above that compared nothing.
#
# This tool writes the squashfs image people run against real seed phrases, so
# it gets the same treatment as the age binary: a fixed version, a checksum
# compared explicitly so a failure prints both values, and the two updated
# together, never one alone.
#
# The repository matters as well as the version. AppImage/appimagetool is the
# maintained home with real semantic versions; AppImage/AppImageKit publishes
# its release-13 assets under 'obsolete-' names.
#
# Checksum taken from PeteSparrowBTC/tails-appimage and confirmed here against a
# fresh download on 2026-08-09.
APPIMAGETOOL_VERSION="1.9.1"
APPIMAGETOOL_SHA256="ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0"
APPIMAGETOOL_URL="https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-x86_64.AppImage"
TOOL="$WORK/appimagetool"

echo "Fetching appimagetool ${APPIMAGETOOL_VERSION}..."
curl -fsSL -o "$TOOL" "$APPIMAGETOOL_URL"

ACTUAL_TOOL_SHA="$(sha256sum "$TOOL" | cut -d' ' -f1)"
if [ "$ACTUAL_TOOL_SHA" != "$APPIMAGETOOL_SHA256" ]; then
  echo "error: appimagetool checksum mismatch"
  echo "  expected $APPIMAGETOOL_SHA256"
  echo "  actual   $ACTUAL_TOOL_SHA"
  exit 1
fi
echo "Verified appimagetool against its pinned checksum."
chmod +x "$TOOL"

# ── Build. --appimage-extract-and-run avoids needing FUSE (WSL/containers).
ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" "$OUTPUT"
echo "built: $OUTPUT"
