#!/usr/bin/env bash
# Build the plugin's package into <dist>: WorldEditorBridge-<version>.zip, from
# tools/thunderstore/bridge/ (manifest.json and the mod page's README.md), the editor's CHANGELOG.md
# (through tools/thunderstore/changelog.py), Desktop/Assets/icon.png, the plugin built here
# (plugins/WorldEditorBridge.dll) and tools/plugin-readme.txt (README.txt, for installing by hand).
# The same zip goes to the GitHub release, Thunderstore and Hexium; the editor itself is on GitHub
# releases only (Thunderstore does not host programs).
# Usage: tools/thunderstore.sh <dist>   (needs dotnet 8, zip, python3; the plugin builds against the
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

# Thunderstore's rules: name a-z A-Z 0-9 _, description up to 250 characters, a 256x256 PNG icon.
check() {
  python3 - "$1" <<'PY'
import json, re, struct, sys
d = sys.argv[1]
m = json.load(open(f"{d}/manifest.json", encoding="utf-8"))
assert re.fullmatch(r"[A-Za-z0-9_]{1,128}", m["name"]), "name"
assert len(m["description"]) <= 250, "description too long"
assert re.fullmatch(r"\d+\.\d+\.\d+", m["version_number"]), "version"
assert all(re.fullmatch(r"\w+-\w+-\d+\.\d+\.\d+", x) for x in m["dependencies"]), "dependencies"
png = open(f"{d}/icon.png", "rb").read(24)
assert png[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", png[16:24]) == (256, 256), "icon must be a 256x256 PNG"
open(f"{d}/README.md", encoding="utf-8").read()
PY
}

package() {   # $1 package name, $2 its folder in tools/thunderstore; the files are already in $work/$1
  local name=$1 dir="$work/$1"
  sed "s/@VERSION@/$version/g" "$ts/$2/manifest.json" > "$dir/manifest.json"
  cp "$ts/$2/README.md" "$repo/Desktop/Assets/icon.png" "$dir/"
  # The editor's changelog, with links to the matching editor on GitHub (versions with a release tag).
  python3 "$ts/changelog.py" "$repo/CHANGELOG.md" "$dir/CHANGELOG.md" "$version" $(git -C "$repo" tag -l 'v*' 2>/dev/null)
  check "$dir"
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

