#!/usr/bin/env bash
# Build the release packages (no source code, no game files) into <dist>, one per system:
#   ValheimWorldEditor-<version>-linux-x64.tar.gz   the app (one program file, editor/Desktop/), its README and
#   ValheimWorldEditor-<version>-win-x64.zip        the empty addons folder with its own README (on Windows
#                                                   with its four native DLLs);
#                                                   it reads the game's look from Valheim itself
#   ValheimWorldEditor-<version>-macos-arm64-experimental.zip
#                                                   ValheimWorldEditor.app (Apple silicon) and its README;
#                                                   signed ad hoc, not yet tried on a Mac
# Usage: tools/release.sh <dist>   (needs the .NET 10 SDK, tar, zip, and rcodesign for the Mac packages:
#   apple-codesign, https://github.com/indygreg/apple-platform-rs, on the PATH or in $RCODESIGN; NO_MACOS=1
#   leaves the Mac packages out)
# The version is the VERSION file's (the editor's and the plugin's); the packages are named v<version>.
# The WorldEditorBridge plugin is released on its own (tools/thunderstore.sh: WorldEditorBridge-<version>.zip,
# on the release page, Thunderstore and Hexium), not inside the editor's packages.
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
version=v$(tr -d '[:space:]' < "$repo/VERSION")
dotnet=${DOTNET:-dotnet}
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

publish() {   # $1 runtime id, $2 output folder, then more dotnet publish options
  local rid=$1 out=$2; shift 2
  # Quiet when it works; when it fails, what dotnet said (its errors and warnings) is shown.
  if ! "$dotnet" publish "$repo/editor/Desktop/ValheimWorldEditor.Desktop.csproj" -c Release -r "$rid" --self-contained \
    -p:DebugType=none -p:DebugSymbols=false "$@" -o "$out" > "$work/publish-$rid.log" 2>&1; then
    echo "Publishing for $rid failed:" >&2
    grep -E ": (error|warning) " "$work/publish-$rid.log" | sort -u >&2 || true
    echo "--- the last lines of dotnet publish:" >&2
    tail -30 "$work/publish-$rid.log" >&2
    exit 1
  fi
}

readme() {   # $1 readme in tools/, $2 where it goes
  cp "$repo/tools/$1" "$2"
  # @VERSION@: v1.2.3 (the editor's packages); @NUMBER@: 1.2.3 (the plugin's zip, WorldEditorBridge-1.2.3.zip).
  sed -i -e "s/@VERSION@/$version/g" -e "s/@NUMBER@/${version#v}/g" "$2"
}

package() {   # $1 runtime id, $2 program file name (users start it by double-clicking), $3 readme
  local rid=$1 exe=$2
  local dir="$work/$rid/ValheimWorldEditor"
  publish "$rid" "$work/$rid/publish" -p:PublishSingleFile=true
  mkdir -p "$dir"
  # The program, and on Windows its native libraries beside it (ANGLE, Skia, HarfBuzz, SPIRV-Cross: not
  # unpacked to a temporary folder at start, see the project file); on Linux they are inside it.
  cp "$work/$rid/publish/$exe" "$dir/"
  if [ "$rid" = win-x64 ]; then cp "$work/$rid/publish/"*.dll "$dir/"; fi
  readme "$3" "$dir/README.txt"
  # The add-ons' folder, next to the program (AddOns), with what to put there.
  mkdir -p "$dir/addons"
  readme release-readme-addons.txt "$dir/addons/README.txt"
  # Windows readers get Windows line ends.
  if [ "$rid" = win-x64 ]; then find "$dir" -maxdepth 2 -name README.txt -exec sed -i 's/$/\r/' {} \;; fi
}

package linux-x64 ValheimWorldEditor release-readme-linux.txt
tar -C "$work/linux-x64" -czf "$dist/ValheimWorldEditor-$version-linux-x64.tar.gz" ValheimWorldEditor
package win-x64 ValheimWorldEditor.exe release-readme-windows.txt
(cd "$work/win-x64" && zip -qr "$dist/ValheimWorldEditor-$version-win-x64.zip" ValheimWorldEditor)

# macOS: ValheimWorldEditor.app, the program published as a folder inside it (Contents/MacOS), not one file: on Apple
# silicon every library must carry a signature, so they stay in the signed bundle instead of being unpacked to a
# temporary folder at start. Signed ad hoc with rcodesign (no Apple Developer certificate: macOS asks once, see the
# README); the icon is editor/Desktop/Assets/icon.icns (icon.png at 128 and 256 pixels).
package_mac() {   # $1 runtime id, $2 the package's name for it
  local rid=$1 name=$2
  local app="$work/$rid/ValheimWorldEditor/ValheimWorldEditor.app"
  publish "$rid" "$app/Contents/MacOS" -p:PublishSingleFile=false -p:SatelliteResourceLanguages=en
  mkdir -p "$app/Contents/Resources"
  sed -e "s/@NUMBER@/${version#v}/g" "$repo/tools/release-macos-Info.plist" > "$app/Contents/Info.plist"
  cp "$repo/editor/Desktop/Assets/icon.icns" "$app/Contents/Resources/"
  readme release-readme-macos.txt "$work/$rid/ValheimWorldEditor/README.txt"
  # The add-ons' folder next to the app (not inside its signed bundle).
  mkdir -p "$work/$rid/ValheimWorldEditor/addons"
  readme release-readme-addons.txt "$work/$rid/ValheimWorldEditor/addons/README.txt"
  if ! "$rcodesign" sign "$app" > "$work/sign-$rid.log" 2>&1; then
    echo "Signing the $rid app failed:" >&2
    tail -30 "$work/sign-$rid.log" >&2
    exit 1
  fi
  (cd "$work/$rid" && zip -qry "$dist/ValheimWorldEditor-$version-$name-experimental.zip" ValheimWorldEditor)
}

if [ "${NO_MACOS:-}" != 1 ]; then
  rcodesign=${RCODESIGN:-$(command -v rcodesign || true)}
  if [ -z "$rcodesign" ]; then
    echo "rcodesign is needed to sign the Mac packages (apple-codesign, https://github.com/indygreg/apple-platform-rs):" >&2
    echo "put it on the PATH or in \$RCODESIGN, or set NO_MACOS=1 to leave the Mac packages out." >&2
    exit 1
  fi
  # Apple silicon only: the Intel build of SPIRV-Cross (libspirv-cross.dylib) has no room left in its header for a
  # signature, and rcodesign cannot leave one file out of a bundle's.
  package_mac osx-arm64 macos-arm64
fi

ls -la "$dist"
