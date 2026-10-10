#!/usr/bin/env bash
# Build the release packages (no source code, no game files) into <dist>, one per system:
#   ValheimWorldEditor-<version>-linux-x64.tar.gz   the app (one program file, Desktop/) and its
#   ValheimWorldEditor-<version>-win-x64.zip        README; it reads the game's look from Valheim itself
# Usage: tools/release.sh <dist>   (needs the .NET 10 SDK, tar, zip)
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

package() {   # $1 runtime id, $2 program file name (users start it by double-clicking), $3 readme
  local rid=$1 exe=$2 readme=$3
  local dir="$work/$rid/ValheimWorldEditor"
  "$dotnet" publish "$repo/Desktop/ValheimWorldEditor.Desktop.csproj" -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o "$work/$rid/publish" >/dev/null
  mkdir -p "$dir"
  # One file: the libraries it needs (Skia, HarfBuzz, ANGLE on Windows) are packed inside it.
  cp "$work/$rid/publish/$exe" "$dir/"
  cp "$repo/tools/$readme" "$dir/README.txt"
  # @VERSION@: v1.2.3 (the editor's packages); @NUMBER@: 1.2.3 (the plugin's zip, WorldEditorBridge-1.2.3.zip).
  sed -i -e "s/@VERSION@/$version/g" -e "s/@NUMBER@/${version#v}/g" "$dir/README.txt"
  # Windows readers get Windows line ends.
  if [ "$rid" = win-x64 ]; then find "$dir" -maxdepth 2 -name README.txt -exec sed -i 's/$/\r/' {} \;; fi
}

package linux-x64 ValheimWorldEditor release-readme-linux.txt
tar -C "$work/linux-x64" -czf "$dist/ValheimWorldEditor-$version-linux-x64.tar.gz" ValheimWorldEditor
package win-x64 ValheimWorldEditor.exe release-readme-windows.txt
(cd "$work/win-x64" && zip -qr "$dist/ValheimWorldEditor-$version-win-x64.zip" ValheimWorldEditor)

ls -la "$dist"
