#!/usr/bin/env bash
# Build the SDK for mod authors into <dist>: ValheimWorldEditor.Sdk-<version>.zip, holding
# ValheimWorldEditor.Sdk.dll (what an editor extension is built against, netstandard2.0; its net10.0 build, with the
# world-rules model add-ons that make world rules need, in net10.0/) and
# WorldEditorBridge.Api.dll (what a mod's own plugin builds against to take part in live mode), each
# with its documentation file (.xml, for code editors), the samples' source (an editor extension and a
# plugin, sdk/samples/ExampleVehicles.*), docs/modding.md as README.md, and the repository's LICENSE (MIT).
# Attached to the GitHub release; see docs/modding.md#extensions.
# Usage: tools/sdk.sh <dist>   (needs the .NET 10 SDK and zip)
set -euo pipefail
dist=$(realpath -m "${1:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
mkdir -p "$dist"

version=$(tr -d '[:space:]' < "$repo/VERSION")
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION is not Major.Minor.Patch" >&2; exit 1; }

"${DOTNET:-dotnet}" build "$repo/sdk/ValheimWorldEditor.Sdk/ValheimWorldEditor.Sdk.csproj" -c Release -f netstandard2.0 -p:DebugType=none -o "$work/build"
"${DOTNET:-dotnet}" build "$repo/sdk/ValheimWorldEditor.Sdk/ValheimWorldEditor.Sdk.csproj" -c Release -f net10.0 -p:DebugType=none -o "$work/build-net10"
"${DOTNET:-dotnet}" build "$repo/sdk/WorldEditorBridge.Api/WorldEditorBridge.Api.csproj" -c Release -p:DebugType=none -o "$work/build"
out="$work/ValheimWorldEditor.Sdk"
mkdir -p "$out/samples/ExampleVehicles.WorldEditor" "$out/samples/ExampleVehicles.Bridge" "$out/net10.0"
cp "$work/build-net10/ValheimWorldEditor.Sdk.dll" "$work/build-net10/ValheimWorldEditor.Sdk.xml" "$out/net10.0/"
cp "$work/build/ValheimWorldEditor.Sdk.dll" "$work/build/ValheimWorldEditor.Sdk.xml" "$work/build/WorldEditorBridge.Api.dll" "$work/build/WorldEditorBridge.Api.xml" "$repo/LICENSE" "$out/"
cp "$repo/docs/modding.md" "$out/README.md"
# The samples' source as in the repository (not what a build left there).
for sample in ExampleVehicles.WorldEditor ExampleVehicles.Bridge; do
  (cd "$repo/sdk/samples/$sample" && git ls-files -z | xargs -0 -I{} cp --parents {} "$out/samples/$sample/")
done
rm -f "$dist/ValheimWorldEditor.Sdk-$version.zip"
(cd "$out" && zip -qrX "$dist/ValheimWorldEditor.Sdk-$version.zip" .)
echo "$dist/ValheimWorldEditor.Sdk-$version.zip"
