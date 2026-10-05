#!/usr/bin/env bash
# Build the release packages (no source code, no game files) into <dist>, one per system:
#   ValheimWorldEditor-<version>-linux-x64.tar.gz   the app (window, web page, game-look exporter
#   ValheimWorldEditor-<version>-win-x64.zip        with its own Python) and plugin/ with
#                                                   WorldEditorBridge.dll for live mode
# Usage: tools/release.sh <version> <dist>   (needs dotnet 8, tar, zip, curl, python3 with pip)
#   SKIP_PLUGIN=1: leave out the plugin (it builds against the game's DLLs, which CI does not have).
set -euo pipefail
version=${1:?version, e.g. v0.1.0}; dist=$(realpath -m "${2:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
dotnet=${DOTNET:-dotnet}
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

# The plugin first (references the game's and BepInEx's DLLs; see its project file): both packages
# carry it.
if [ "${SKIP_PLUGIN:-}" != 1 ]; then
  "$dotnet" build "$repo/plugin/WorldEditorBridge/WorldEditorBridge.csproj" -c Release -p:DebugType=none -o "$work/plugin" >/dev/null
fi

package() {   # $1 runtime id, $2 program file name (users start it by double-clicking), $3 readme
  local rid=$1 exe=$2 readme=$3
  local dir="$work/$rid/ValheimWorldEditor"
  "$dotnet" publish "$repo/TerrainEditor.csproj" -c Release -r "$rid" --self-contained -p:PublishSingleFile=true \
    -p:DebugType=none -o "$work/$rid/publish" >/dev/null
  mkdir -p "$dir/export-game-files"
  cp "$work/$rid/publish/$exe" "$dir/"
  # Photino's native window library (single-file publish keeps native files next to the program).
  find "$work/$rid/publish" -maxdepth 1 \( -name '*.so' -o -name '*.dll' \) -exec cp {} "$dir/" \;
  cp -r "$work/$rid/publish/wwwroot" "$dir/"
  # Never ship files extracted from the game, even if a local build folder had them.
  rm -rf "$dir/wwwroot/models" "$dir/wwwroot/maptex" "$dir/wwwroot/terrain/"*.png "$dir/wwwroot/terrain/heightmap.frag.glsl"
  cp "$repo"/tools/asset-export/{export_all.py,assetlib.py,export_pieces.py,fix_normals.py,fix_alpha.py,requirements.txt} \
     "$repo/tools/zdo_scan.py" "$repo/WorldGen/pieces.json" "$dir/export-game-files/"
  "$repo/tools/make-python-runtime.sh" "$rid" "$dir/export-game-files" >/dev/null
  cp "$repo/tools/$readme" "$dir/README.txt"
  sed -i "s/@VERSION@/$version/" "$dir/README.txt"
  if [ "${SKIP_PLUGIN:-}" != 1 ]; then
    mkdir -p "$dir/plugin"
    cp "$work/plugin/WorldEditorBridge.dll" "$dir/plugin/"
    cp "$repo/tools/plugin-readme.txt" "$dir/plugin/README.txt"
    sed -i "s/@VERSION@/$version/" "$dir/plugin/README.txt"
  fi
  # Windows readers get Windows line ends.
  if [ "$rid" = win-x64 ]; then find "$dir" -maxdepth 2 -name README.txt -exec sed -i 's/$/\r/' {} \;; fi
}

package linux-x64 ValheimWorldEditor release-readme-linux.txt
tar -C "$work/linux-x64" -czf "$dist/ValheimWorldEditor-$version-linux-x64.tar.gz" ValheimWorldEditor
package win-x64 ValheimWorldEditor.exe release-readme-windows.txt
(cd "$work/win-x64" && zip -qr "$dist/ValheimWorldEditor-$version-win-x64.zip" ValheimWorldEditor)

rm -rf "$repo/bin" "$repo/obj" "$repo/plugin/WorldEditorBridge/bin" "$repo/plugin/WorldEditorBridge/obj"
ls -la "$dist"
