#!/usr/bin/env bash
# Build the SDK for mod authors into <dist>: ValheimWorldEditor.Sdk-<version>.zip, holding
# ValheimWorldEditor.Sdk.dll (what an editor extension is built against, netstandard2.0) with its
# documentation file (ValheimWorldEditor.Sdk.xml, for code editors), the sample extension's source
# (samples/ValheimRAFT.WorldEditor), docs/modding.md as README.md, and the repository's LICENSE (MIT).
# Attached to the GitHub release; see docs/modding.md#extensions.
# Usage: tools/sdk.sh <dist>   (needs the .NET 10 SDK and zip)
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

version=$(tr -d '[:space:]' < "$repo/VERSION")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION is not Major.Minor.Patch" >&2; exit 1; }

"${DOTNET:-dotnet}" build "$repo/Sdk/ValheimWorldEditor.Sdk.csproj" -c Release -p:DebugType=none -o "$work/build"
out="$work/ValheimWorldEditor.Sdk"
mkdir -p "$out/samples/ValheimRAFT.WorldEditor"
cp "$work/build/ValheimWorldEditor.Sdk.dll" "$work/build/ValheimWorldEditor.Sdk.xml" "$repo/LICENSE" "$out/"
cp "$repo/docs/modding.md" "$out/README.md"
# The sample's source as in the repository (not what a build left there).
(cd "$repo/samples/ValheimRAFT.WorldEditor" && git ls-files -z | xargs -0 -I{} cp --parents {} "$out/samples/ValheimRAFT.WorldEditor/")
rm -f "$dist/ValheimWorldEditor.Sdk-$version.zip"
(cd "$out" && zip -qrX "$dist/ValheimWorldEditor.Sdk-$version.zip" .)
echo "$dist/ValheimWorldEditor.Sdk-$version.zip"
