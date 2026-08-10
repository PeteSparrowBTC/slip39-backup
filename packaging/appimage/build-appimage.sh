#!/usr/bin/env bash
# Builds slip39-backup-x86_64.AppImage: the offline, native-window artifact for
# Tails 7+. Run on Linux (a CI ubuntu runner, or WSL for local builds).
#
# Usage:
#   ./build-appimage.sh <tauri-release-binary> <output.AppImage>
#
# The binary comes from:
#   dotnet publish Slip39Demo.Tauri -c Release -o publish-tauri
#   cargo tauri build --no-bundle --manifest-path src-tauri/Cargo.toml
#
# The output name carries the version, for example
# slip39-backup-2.0.0-x86_64.AppImage. The caller supplies it rather than this script
# deriving it, so there is one place that decides what the version is: appimage.yml
# takes it from the tag or from Directory.Build.props and uses the same value for
# `dotnet publish -p:Version=` and for this filename. A name and a window footer that
# disagree would be worse than neither carrying a version at all.
#
# System libraries (webkit2gtk-4.1, gtk3) are NOT bundled. Tails ships them, and
# bundling a browser engine known to be present triples the size and pins a rendering
# stack. See PeteSparrowBTC/tails-appimage.
set -euo pipefail

BINARY="${1:?usage: build-appimage.sh <tauri-release-binary> <output.AppImage>}"
OUTPUT="${2:?usage: build-appimage.sh <tauri-release-binary> <output.AppImage>}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

[ -x "$BINARY" ] || { echo "error: $BINARY missing or not executable"; exit 1; }

# The check that matters on Tails. Debian 13 dropped the webkit2gtk-4.0 series, so a
# binary linking libwebkit2gtk-4.0.so.37 will not start there, and the failure looks
# like an application bug rather than a packaging one. Fail here instead.
NEEDED="$(readelf -d "$BINARY" | grep NEEDED || true)"
if echo "$NEEDED" | grep -q "libwebkit2gtk-4\.0"; then
  echo "error: $BINARY links webkit2gtk-4.0, which Tails 7 does not ship"; exit 1
fi
if ! echo "$NEEDED" | grep -q "libwebkit2gtk-4\.1"; then
  echo "error: $BINARY does not link webkit2gtk-4.1; check the build environment"; exit 1
fi
echo "Links webkit2gtk-4.1, which Tails ships."

# ── Assemble the AppDir ────────────────────────────────────────────────
APPDIR="$WORK/AppDir"
mkdir -p "$APPDIR/usr/bin"
cp "$SCRIPT_DIR/AppRun" "$APPDIR/AppRun"
cp "$SCRIPT_DIR/slip39-backup.desktop" "$APPDIR/"
cp "$SCRIPT_DIR/../../Slip39Demo.UI/wwwroot/favicon.png" "$APPDIR/slip39-backup.png"
cp "$BINARY" "$APPDIR/usr/bin/slip39-backup"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/slip39-backup"

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
