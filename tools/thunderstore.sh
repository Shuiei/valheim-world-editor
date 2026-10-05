#!/usr/bin/env bash
# Build the Thunderstore package of the WorldEditorBridge plugin into <dist>:
#   <dist>/WorldEditorBridge-<version>.zip   manifest.json, icon.png, README.md, CHANGELOG.md and
#                                            WorldEditorBridge.dll, all at the root of the zip
# The version is the plugin's own (WorldEditorBridgePlugin.Version), not the editor's.
# Usage: tools/thunderstore.sh <dist>   (needs dotnet 8, zip, python3 and the game's DLLs; see the
# plugin's project file). Upload the zip at https://thunderstore.io/c/valheim/create/
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
dotnet=${DOTNET:-dotnet}
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

version=$(sed -n 's/.*const string Version = "\([0-9.]*\)".*/\1/p' "$repo/plugin/WorldEditorBridge/WorldEditorBridgePlugin.cs")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "plugin version not found" >&2; exit 1; }

"$dotnet" build "$repo/plugin/WorldEditorBridge/WorldEditorBridge.csproj" -c Release -p:DebugType=none -o "$work/build" >/dev/null
rm -rf "$repo/plugin/WorldEditorBridge/bin" "$repo/plugin/WorldEditorBridge/obj"

mkdir "$work/pkg"
cp "$work/build/WorldEditorBridge.dll" "$repo/wwwroot/icon.png" "$repo/tools/thunderstore/README.md" "$work/pkg/"
sed "s/@VERSION@/$version/" "$repo/tools/thunderstore/manifest.json" > "$work/pkg/manifest.json"
cp "$repo/tools/thunderstore/CHANGELOG.md" "$work/pkg/CHANGELOG.md"

# Thunderstore's rules: name a-z A-Z 0-9 _, description up to 250 characters, a 256x256 PNG icon.
python3 - "$work/pkg" <<'PY'
import json, re, struct, sys
d = sys.argv[1]
m = json.load(open(f"{d}/manifest.json", encoding="utf-8"))
assert re.fullmatch(r"[A-Za-z0-9_]{1,128}", m["name"]), "name"
assert len(m["description"]) <= 250, "description too long"
assert re.fullmatch(r"\d+\.\d+\.\d+", m["version_number"]), "version"
assert all(re.fullmatch(r"[\w]+-[\w]+-\d+\.\d+\.\d+", x) for x in m["dependencies"]), "dependencies"
png = open(f"{d}/icon.png", "rb").read(24)
assert png[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", png[16:24]) == (256, 256), "icon must be a 256x256 PNG"
open(f"{d}/README.md", encoding="utf-8").read()
PY

out="$dist/WorldEditorBridge-$version.zip"
rm -f "$out"
(cd "$work/pkg" && zip -q -X "$out" manifest.json icon.png README.md CHANGELOG.md WorldEditorBridge.dll)
echo "$out"
