#!/bin/sh
# Builds a self-contained .deb for the given architecture (amd64 or arm64).
# Usage: packaging/build-deb.sh [amd64|arm64] [version]
set -eu
cd "$(dirname "$0")/.."
arch="${1:-amd64}"
version="${2:-0.1.0}"
case "$arch" in
  amd64) rid=linux-x64 ;;
  arm64) rid=linux-arm64 ;;
  *) echo "unknown architecture: $arch" >&2; exit 2 ;;
esac
name=commons-diversified-uploader
root="build/deb/${name}_${version}_${arch}"
rm -rf "$root"
mkdir -p "$root/DEBIAN" "$root/opt/$name" "$root/usr/bin" "$root/usr/share/applications" \
  "$root/usr/share/icons/hicolor/256x256/apps"

dotnet publish src/Uploader -c Release -r "$rid" --self-contained true \
  -p:DebugType=none -p:Version="$version" -o "$root/opt/$name"
ln -s "/opt/$name/$name" "$root/usr/bin/$name"
cp data/tr.tall.CommonsDiversifiedUploader.desktop "$root/usr/share/applications/"
cp data/tr.tall.CommonsDiversifiedUploader.png "$root/usr/share/icons/hicolor/256x256/apps/"

size=$(du -sk "$root" | cut -f1)
cat > "$root/DEBIAN/control" <<CONTROL
Package: $name
Version: $version
Architecture: $arch
Maintainer: Commons Diversified Uploader contributors <noreply@users.noreply.github.com>
Installed-Size: $size
Depends: libgtk-4-1 (>= 4.12), libadwaita-1-0 (>= 1.5), libsecret-tools, xdg-utils
Section: graphics
Priority: optional
Homepage: https://github.com/HakanIST/commons-diversified-uploader-linux
Description: Upload match photos to Wikimedia Commons by jersey number
 Reads jersey numbers and jersey colours from sports photos on the device,
 maps them to players through the team rosters on Wikipedia and Wikidata,
 proposes the matching Commons categories and uploads the reviewed photos.
 .
 Community project; not affiliated with the Wikimedia Foundation.
CONTROL
cat > "$root/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q /usr/share/applications || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true; fi
POSTINST
cp "$root/DEBIAN/postinst" "$root/DEBIAN/postrm"
chmod 755 "$root/DEBIAN/postinst" "$root/DEBIAN/postrm"
dpkg-deb --build --root-owner-group "$root" "build/${name}_${version}_${arch}.deb"
