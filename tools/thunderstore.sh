#!/usr/bin/env bash
# Build the Thunderstore packages into <dist>, from tools/thunderstore/<package>/ (manifest.json and
# the mod page's README.md), tools/thunderstore/CHANGELOG.md and wwwroot/icon.png:
#   WorldEditorBridge-<version>.zip               the plugin alone (servers)
#   ValheimWorldEditor_Windows-<version>.zip      the editor for Windows, with the plugin
#   ValheimWorldEditor_Linux-<version>.zip        the editor for Linux, with the plugin
# The editor packages are the release packages (tools/release.sh) with the ValheimWorldEditor folder
# under plugins/: mod managers keep the folders inside plugins/ and flatten any other. The version
# is the VERSION file's. Release packages of that version already in <dist> are reused.
# Usage: tools/thunderstore.sh <dist>   (needs what tools/release.sh needs)
# Upload each zip at https://thunderstore.io/c/valheim/create/
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
ts="$repo/tools/thunderstore"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

version=$(tr -d '[:space:]' < "$repo/VERSION")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION is not Major.Minor.Patch" >&2; exit 1; }
linux="$dist/ValheimWorldEditor-v$version-linux-x64.tar.gz" windows="$dist/ValheimWorldEditor-v$version-win-x64.zip"
[ -f "$linux" ] && [ -f "$windows" ] || "$repo/tools/release.sh" "$dist" >/dev/null

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
  sed "s/@VERSION@/$version/" "$ts/$2/manifest.json" > "$dir/manifest.json"
  cp "$ts/$2/README.md" "$ts/CHANGELOG.md" "$repo/wwwroot/icon.png" "$dir/"
  check "$dir"
  rm -f "$dist/$name-$version.zip"
  (cd "$dir" && zip -qrX "$dist/$name-$version.zip" .)
  echo "$dist/$name-$version.zip"
}

# The plugin alone: the same DLL as in the editor packages.
mkdir -p "$work/WorldEditorBridge/plugins"
tar -xzf "$linux" -C "$work" ValheimWorldEditor/plugin/WorldEditorBridge.dll
mv "$work/ValheimWorldEditor/plugin/WorldEditorBridge.dll" "$work/WorldEditorBridge/plugins/"
rm -rf "$work/ValheimWorldEditor"
package WorldEditorBridge bridge

mkdir -p "$work/ValheimWorldEditor_Linux/plugins" "$work/ValheimWorldEditor_Windows/plugins"
tar -xzf "$linux" -C "$work/ValheimWorldEditor_Linux/plugins"
unzip -q "$windows" -d "$work/ValheimWorldEditor_Windows/plugins"
package ValheimWorldEditor_Linux linux
package ValheimWorldEditor_Windows windows
