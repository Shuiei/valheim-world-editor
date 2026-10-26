#!/usr/bin/env bash
# Builds every add-on (addons/<Name>/) and zips each as players install it, a <Name> folder to unpack into the
# editor's addons folder: its extension DLL, its worldeditor.json, its package-readme.txt as README.txt and the
# repository's LICENSE. Each zip is named after the add-on and its own version (addons/<Name>/VERSION):
# ValheimWorldEditor-addon-<Name>-<version>.zip, one release asset each beside the editor's packages.
# Usage: tools/addons.sh <dist>
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"
for dir in "$repo"/addons/*/; do
  name=$(basename "$dir")
  version=$(tr -d '[:space:]' < "$dir/VERSION")
  [[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "addons/$name/VERSION is not Major.Minor.Patch" >&2; exit 1; }
  projects=("$dir"src/*/*.csproj)
  [ ${#projects[@]} -eq 1 ] || { echo "addons/$name/src holds ${#projects[@]} projects, not one" >&2; exit 1; }
  "${DOTNET:-dotnet}" build "${projects[0]}" -c Release -p:DebugType=none -o "$work/build/$name" > "$work/build-$name.log" 2>&1 \
    || { grep -E ": (error|warning) " "$work/build-$name.log" | sort -u >&2; exit 1; }
  # The extension's file name, wherever the manifest puts spaces or tabs around its colon.
  dll=$(tr -d '\n\r' < "$work/build/$name/worldeditor.json" | sed -n 's/.*"extension"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')
  [ -f "$work/build/$name/$dll" ] || { echo "addons/$name: its worldeditor.json names $dll, which the build did not make" >&2; exit 1; }
  out="$work/zip/$name"
  mkdir -p "$out"
  cp "$work/build/$name/$dll" "$work/build/$name/worldeditor.json" "$repo/LICENSE" "$out/"
  cp "$dir/package-readme.txt" "$out/README.txt"
  zip="$dist/ValheimWorldEditor-addon-$name-$version.zip"
  rm -f "$zip"
  (cd "$work/zip" && zip -qrX "$zip" "$name")
  echo "$zip"
done
