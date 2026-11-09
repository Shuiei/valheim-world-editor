#!/usr/bin/env bash
# Build the plugins' packages into <dist>: WorldEditorBridge-<version>.zip, from
# tools/thunderstore/bridge/ (manifest.json and the mod page's README.md), every release's notes as its GitHub
# release has them (through tools/thunderstore/package.cs), editor/Desktop/Assets/icon.png, the plugin built here
# (plugins/WorldEditorBridge.dll, with plugins/WorldEditorBridge.Api.dll, what mods' plugins register with:
# docs/modding.md#live-mode), tools/plugin-readme.txt (README.txt, for installing by hand) and the
# repository's LICENSE (MIT). Then DrawnWorlds-<version>.zip the same way, from tools/thunderstore/drawn/ (the mod
# drawn worlds need in the game: plugins/DrawnWorlds.dll, tools/drawn-readme.txt as README.txt).
# The same zip goes to the GitHub release, Thunderstore and Hexium; the editor itself is on GitHub
# releases only (Thunderstore does not host programs).
# Usage: ValheimDir=<server> tools/thunderstore.sh <dist>   (needs the .NET 10 SDK and zip; the plugin builds against the
# game's and BepInEx's DLLs, see its project file)
# Upload the zip at https://thunderstore.io/c/valheim/create/
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
ts="$repo/tools/thunderstore"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

[[ -n ${ValheimDir:-} ]] || { echo "Set ValheimDir to a Valheim dedicated server's folder (and BepInExDir if its BepInEx is elsewhere): the plugin builds against their DLLs" >&2; exit 1; }
version=$(tr -d '[:space:]' < "$repo/VERSION")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION is not Major.Minor.Patch" >&2; exit 1; }

# tools/thunderstore/package.cs (a .NET file-based app): the changelog, and Thunderstore's rules.
pkg() { "${DOTNET:-dotnet}" run "$ts/package.cs" -- "$@"; }

package() {   # $1 package name, $2 its folder in tools/thunderstore; the files are already in $work/$1
  local name=$1 dir="$work/$1"
  sed "s/@VERSION@/$version/g" "$ts/$2/manifest.json" > "$dir/manifest.json"
  cp "$ts/$2/README.md" "$repo/editor/Desktop/Assets/icon.png" "$repo/LICENSE" "$dir/"
  # The plugin's changelog: every release's notes as its GitHub release has them (each tag's, and this version's),
  # with links to the matching editor on GitHub.
  local tags
  tags=$(git -C "$repo" tag -l 'v*')
  (cd "$repo" && "${DOTNET:-dotnet}" run tools/release/publish.cs -- notes-all CHANGELOG.md docs/releases "$work/notes" "$version" $tags)
  pkg changelog "$repo/CHANGELOG.md" "$work/notes" "$dir/CHANGELOG.md" "$version"
  pkg check "$dir"
  rm -f "$dist/$name-$version.zip"
  (cd "$dir" && zip -qrX "$dist/$name-$version.zip" .)
  echo "$dist/$name-$version.zip"
}

mkdir -p "$work/WorldEditorBridge/plugins"
"${DOTNET:-dotnet}" build "$repo/editor/plugin/WorldEditorBridge/WorldEditorBridge.csproj" -c Release -p:DebugType=none -o "$work/build" >/dev/null
cp "$work/build/WorldEditorBridge.dll" "$work/build/WorldEditorBridge.Api.dll" "$work/WorldEditorBridge/plugins/"
sed "s/@VERSION@/$version/g" "$repo/tools/plugin-readme.txt" > "$work/WorldEditorBridge/README.txt"
rm -rf "$repo/editor/plugin/WorldEditorBridge/bin" "$repo/editor/plugin/WorldEditorBridge/obj"
package WorldEditorBridge bridge

mkdir -p "$work/DrawnWorlds/plugins"
"${DOTNET:-dotnet}" build "$repo/editor/plugin/DrawnWorlds/DrawnWorlds.csproj" -c Release -p:DebugType=none -o "$work/drawn" >/dev/null
cp "$work/drawn/DrawnWorlds.dll" "$work/DrawnWorlds/plugins/"
sed "s/@VERSION@/$version/g" "$repo/tools/drawn-readme.txt" > "$work/DrawnWorlds/README.txt"
rm -rf "$repo/editor/plugin/DrawnWorlds/bin" "$repo/editor/plugin/DrawnWorlds/obj"
package DrawnWorlds drawn
