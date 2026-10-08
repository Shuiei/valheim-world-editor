#!/usr/bin/env bash
# Build the Thunderstore package of the plugin into <dist>: WorldEditorBridge-<version>.zip, from
# tools/thunderstore/bridge/ (manifest.json and the mod page's README.md), the editor's CHANGELOG.md
# (through tools/thunderstore/changelog.py), Desktop/Assets/icon.png and the DLL of the release packages
# (tools/release.sh; those of the VERSION file's version already in <dist> are reused). Only the
# plugin goes to Thunderstore: it does not host programs, so the editor is on GitHub releases only.
# Usage: tools/thunderstore.sh <dist>   (needs what tools/release.sh needs)
# Upload the zip at https://thunderstore.io/c/valheim/create/
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
  sed "s/@VERSION@/$version/g" "$ts/$2/manifest.json" > "$dir/manifest.json"
  cp "$ts/$2/README.md" "$repo/Desktop/Assets/icon.png" "$dir/"
  # The editor's changelog, with links to the matching editor on GitHub (versions with a release tag).
  python3 "$ts/changelog.py" "$repo/CHANGELOG.md" "$dir/CHANGELOG.md" "$version" $(git -C "$repo" tag -l 'v*' 2>/dev/null)
  check "$dir"
  rm -f "$dist/$name-$version.zip"
  (cd "$dir" && zip -qrX "$dist/$name-$version.zip" .)
  echo "$dist/$name-$version.zip"
}

# The same DLL as in the release packages' plugin/ folder.
mkdir -p "$work/WorldEditorBridge/plugins"
tar -xzf "$linux" -C "$work" ValheimWorldEditor/plugin/WorldEditorBridge.dll
mv "$work/ValheimWorldEditor/plugin/WorldEditorBridge.dll" "$work/WorldEditorBridge/plugins/"
rm -rf "$work/ValheimWorldEditor"
package WorldEditorBridge bridge

