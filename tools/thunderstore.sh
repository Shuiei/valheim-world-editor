#!/usr/bin/env bash
# Build the plugin's package into <dist>: WorldEditorBridge-<version>.zip, from
# tools/thunderstore/bridge/ (manifest.json and the mod page's README.md), the editor's CHANGELOG.md
# (through tools/thunderstore/package.cs), Desktop/Assets/icon.png, the plugin built here
# (plugins/WorldEditorBridge.dll), tools/plugin-readme.txt (README.txt, for installing by hand) and the
# repository's LICENSE (MIT).
# The same zip goes to the GitHub release, Thunderstore and Hexium; the editor itself is on GitHub
# releases only (Thunderstore does not host programs).
# Usage: tools/thunderstore.sh <dist>   (needs the .NET 10 SDK and zip; the plugin builds against the
# game's and BepInEx's DLLs, see its project file)
# Upload the zip at https://thunderstore.io/c/valheim/create/
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
ts="$repo/tools/thunderstore"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

version=$(tr -d '[:space:]' < "$repo/VERSION")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION is not Major.Minor.Patch" >&2; exit 1; }

# tools/thunderstore/package.cs (a .NET file-based app): the changelog, and Thunderstore's rules.
pkg() { "${DOTNET:-dotnet}" run "$ts/package.cs" -- "$@"; }

package() {   # $1 package name, $2 its folder in tools/thunderstore; the files are already in $work/$1
  local name=$1 dir="$work/$1"
  sed "s/@VERSION@/$version/g" "$ts/$2/manifest.json" > "$dir/manifest.json"
  cp "$ts/$2/README.md" "$repo/Desktop/Assets/icon.png" "$repo/LICENSE" "$dir/"
  # The editor's changelog, with links to the matching editor on GitHub (versions with a release tag).
  pkg changelog "$repo/CHANGELOG.md" "$dir/CHANGELOG.md" "$version" $(git -C "$repo" tag -l 'v*' 2>/dev/null)
  pkg check "$dir"
  rm -f "$dist/$name-$version.zip"
  (cd "$dir" && zip -qrX "$dist/$name-$version.zip" .)
  echo "$dist/$name-$version.zip"
}

mkdir -p "$work/WorldEditorBridge/plugins"
"${DOTNET:-dotnet}" build "$repo/plugin/WorldEditorBridge/WorldEditorBridge.csproj" -c Release -p:DebugType=none -o "$work/build" >/dev/null
cp "$work/build/WorldEditorBridge.dll" "$work/WorldEditorBridge/plugins/"
sed "s/@VERSION@/$version/g" "$repo/tools/plugin-readme.txt" > "$work/WorldEditorBridge/README.txt"
rm -rf "$repo/plugin/WorldEditorBridge/bin" "$repo/plugin/WorldEditorBridge/obj"
package WorldEditorBridge bridge

