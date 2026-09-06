#!/usr/bin/env bash
#
# Builds Linux artifacts for Llama Launcher:
#   * a portable  dist/LlamaLauncher-<ver>-<rid>.tar.gz
#   * a Debian    dist/llama-launcher_<ver>_<arch>.deb   (needs dpkg-deb)
#
# Usage (from the repo root):
#   ./packaging/linux/build-linux.sh                 # linux-x64, version 1.0.0
#   ./packaging/linux/build-linux.sh 1.2.0 linux-arm64
#
# Prereqs: .NET 8 SDK; dpkg-deb (from the 'dpkg' package) for the .deb.
set -euo pipefail

VERSION="${1:-1.0.0}"
RID="${2:-linux-x64}"
case "$RID" in
  linux-x64)   DEB_ARCH="amd64" ;;
  linux-arm64) DEB_ARCH="arm64" ;;
  *)           DEB_ARCH="amd64" ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/LlamaLauncher.csproj"
BUILD="$REPO_ROOT/build/$RID"
DIST="$REPO_ROOT/dist"
APPID="llama-launcher"

echo "==> Publishing $RID (self-contained) ..."
rm -rf "$BUILD"
dotnet publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:Version="$VERSION" \
  -p:PublishSingleFile=false \
  -o "$BUILD"

# The Linux ELF apphost has no embedded icon; make sure it is executable.
chmod +x "$BUILD/LlamaLauncher" || true

mkdir -p "$DIST"

# ---------- portable tarball ----------
echo "==> Building tarball ..."
TARROOT="$REPO_ROOT/build/tar/$APPID"
rm -rf "$TARROOT"; mkdir -p "$TARROOT"
cp -r "$BUILD/." "$TARROOT/"
cp "$SCRIPT_DIR/app.png" "$TARROOT/app.png"
cat > "$TARROOT/README-run.txt" <<EOF
Run:  ./LlamaLauncher
On first launch, edit the top-bar paths (llama-server binary, models folder,
opencode.json, config.ini) to match your system.
EOF
tar -C "$REPO_ROOT/build/tar" -czf "$DIST/LlamaLauncher-$VERSION-$RID.tar.gz" "$APPID"
echo "    -> dist/LlamaLauncher-$VERSION-$RID.tar.gz"

# ---------- .deb ----------
if ! command -v dpkg-deb >/dev/null 2>&1; then
  echo "!! dpkg-deb not found; skipping .deb (tarball is ready)."
  exit 0
fi

echo "==> Building .deb ..."
PKG="$REPO_ROOT/build/deb/${APPID}_${VERSION}_${DEB_ARCH}"
rm -rf "$PKG"
mkdir -p "$PKG/DEBIAN" \
         "$PKG/opt/$APPID" \
         "$PKG/usr/bin" \
         "$PKG/usr/share/applications" \
         "$PKG/usr/share/icons/hicolor/256x256/apps" \
         "$PKG/usr/share/icons/hicolor/128x128/apps" \
         "$PKG/usr/share/icons/hicolor/64x64/apps" \
         "$PKG/usr/share/icons/hicolor/48x48/apps"

cp -r "$BUILD/." "$PKG/opt/$APPID/"
chmod +x "$PKG/opt/$APPID/LlamaLauncher"

# Launcher on PATH
ln -sf "/opt/$APPID/LlamaLauncher" "$PKG/usr/bin/$APPID"

# Desktop entry + icons
cp "$SCRIPT_DIR/$APPID.desktop" "$PKG/usr/share/applications/$APPID.desktop"
cp "$SCRIPT_DIR/icons/app-256.png" "$PKG/usr/share/icons/hicolor/256x256/apps/$APPID.png"
cp "$SCRIPT_DIR/icons/app-128.png" "$PKG/usr/share/icons/hicolor/128x128/apps/$APPID.png"
cp "$SCRIPT_DIR/icons/app-64.png"  "$PKG/usr/share/icons/hicolor/64x64/apps/$APPID.png"
cp "$SCRIPT_DIR/icons/app-48.png"  "$PKG/usr/share/icons/hicolor/48x48/apps/$APPID.png"

# Installed size in KB
INSTALLED_KB=$(du -sk "$PKG/opt" | cut -f1)

cat > "$PKG/DEBIAN/control" <<EOF
Package: $APPID
Version: $VERSION
Section: utils
Priority: optional
Architecture: $DEB_ARCH
Maintainer: AptusWorks LLC <maintainer@example.com>
Installed-Size: $INSTALLED_KB
Depends: libc6
Description: Manager for llama-server model launch profiles
 Cross-platform desktop app to manage llama-server (llama.cpp) model launch
 profiles. Stores per-model settings, launches the server, and exports a native
 llama.cpp INI preset so models can also be launched directly from llama-server.
EOF

cat > "$PKG/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database -q /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q /usr/share/icons/hicolor || true
fi
EOF
chmod 0755 "$PKG/DEBIAN/postinst"

dpkg-deb --build --root-owner-group "$PKG" >/dev/null
mv "$PKG.deb" "$DIST/${APPID}_${VERSION}_${DEB_ARCH}.deb"
echo "    -> dist/${APPID}_${VERSION}_${DEB_ARCH}.deb"
echo "Done."
