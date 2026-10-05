#!/usr/bin/env bash
# Build the release packages (no source code, no game files) into <dist>:
#   ValheimWorldEditor-<version>-linux-x64.tar.gz   editor program + web page + game-file export tool
#   ValheimWorldEditor-<version>-win-x64.zip        the same for Windows
#   WorldEditorBridge.dll                           the server plugin for live mode
# Usage: tools/release.sh <version> <dist>   (needs dotnet 8, tar, zip)
set -euo pipefail
version=${1:?version, e.g. v0.1.0}; dist=$(realpath -m "${2:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
dotnet=${DOTNET:-dotnet}
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

package() {   # $1 runtime id, $2 program file name
  local rid=$1 exe=$2
  local dir="$work/$rid/ValheimWorldEditor"
  "$dotnet" publish "$repo/TerrainEditor.csproj" -c Release -r "$rid" --self-contained -p:PublishSingleFile=true \
    -p:DebugType=none -o "$work/$rid/publish" >/dev/null
  mkdir -p "$dir/export-game-files"
  cp "$work/$rid/publish/$exe" "$dir/"
  cp -r "$work/$rid/publish/wwwroot" "$dir/"
  # Never ship files extracted from the game, even if a local build folder had them.
  rm -rf "$dir/wwwroot/models" "$dir/wwwroot/maptex" "$dir/wwwroot/terrain/"*.png "$dir/wwwroot/terrain/heightmap.frag.glsl"
  cp "$repo"/tools/asset-export/{export_all.py,assetlib.py,export_pieces.py,fix_normals.py,fix_alpha.py,requirements.txt} \
     "$repo/tools/zdo_scan.py" "$repo/WorldGen/pieces.json" "$dir/export-game-files/"
  cp "$repo/tools/release-readme.txt" "$dir/README.txt"
  sed -i "s/@VERSION@/$version/" "$dir/README.txt"
}

package linux-x64 ValheimTerrainEditor
tar -C "$work/linux-x64" -czf "$dist/ValheimWorldEditor-$version-linux-x64.tar.gz" ValheimWorldEditor
package win-x64 ValheimTerrainEditor.exe
(cd "$work/win-x64" && zip -qr "$dist/ValheimWorldEditor-$version-win-x64.zip" ValheimWorldEditor)

# The plugin (references the game's and BepInEx's DLLs; see its project file).
"$dotnet" build "$repo/plugin/WorldEditorBridge/WorldEditorBridge.csproj" -c Release -p:DebugType=none -o "$work/plugin" >/dev/null
cp "$work/plugin/WorldEditorBridge.dll" "$dist/"
rm -rf "$repo/bin" "$repo/obj" "$repo/plugin/WorldEditorBridge/bin" "$repo/plugin/WorldEditorBridge/obj"
ls -la "$dist"
