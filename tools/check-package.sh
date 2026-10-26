#!/usr/bin/env bash
# Checks a release package: what must be in it, and what must never be (source code, debug files,
# files extracted from the game, the plugin: it is released on its own). The SDK's zip (tools/sdk.sh) is checked for
# its layout: both builds of the SDK (netstandard2.0 at the root, net10.0 in net10.0/), the bridge API, the samples.
# Usage: tools/check-package.sh <package.tar.gz|.zip>
set -euo pipefail
pkg=${1:?package}
if [[ "$(basename "$pkg")" == ValheimWorldEditor.Sdk-*.zip ]]; then
  list=$(unzip -Z1 "$pkg"); fail=0
  for f in ValheimWorldEditor.Sdk.dll ValheimWorldEditor.Sdk.xml net10.0/ValheimWorldEditor.Sdk.dll net10.0/ValheimWorldEditor.Sdk.xml \
    WorldEditorBridge.Api.dll WorldEditorBridge.Api.xml README.md LICENSE samples/ExampleVehicles.WorldEditor/worldeditor.json \
    samples/ExampleVehicles.Bridge/ExampleVehicles.Bridge.csproj; do
    grep -qx "$f" <<<"$list" || { echo "MISSING  $f"; fail=1; }
  done
  bad=$(grep -E '\.pdb$|/obj/|/bin/' <<<"$list" || true)
  if [ -n "$bad" ]; then echo "FORBIDDEN in the SDK:"; echo "$bad" | head -20; fail=1; fi
  [ $fail = 0 ] && echo "ok  $(basename "$pkg") ($(wc -l <<<"$list") entries)"
  exit $fail
fi
case "$pkg" in
  *.tar.gz) list=$(tar -tzf "$pkg"); exe=ValheimWorldEditor/ValheimWorldEditor ;;
  *-macos-*.zip) list=$(unzip -Z1 "$pkg"); exe=ValheimWorldEditor/ValheimWorldEditor.app/Contents/MacOS/ValheimWorldEditor ;;
  *.zip) list=$(unzip -Z1 "$pkg"); exe=ValheimWorldEditor/ValheimWorldEditor.exe ;;
  *) echo "unknown package type: $pkg" >&2; exit 2 ;;
esac
fail=0
need() { grep -qx "$1" <<<"$list" || { echo "MISSING  $1"; fail=1; }; }
for f in "$exe" ValheimWorldEditor/README.txt ValheimWorldEditor/addons/README.txt; do need "$f"; done
# macOS: the app's description, icon and signature, and its native libraries beside the program.
if [[ "$pkg" == *-macos-*.zip ]]; then
  app=ValheimWorldEditor/ValheimWorldEditor.app/Contents
  for f in Info.plist Resources/icon.icns _CodeSignature/CodeResources MacOS/libAvaloniaNative.dylib MacOS/libSkiaSharp.dylib \
    MacOS/libHarfBuzzSharp.dylib MacOS/libspirv-cross.dylib; do need "$app/$f"; done
# Windows: the program's native libraries beside it (they are not unpacked at start).
elif [[ "$pkg" == *.zip ]]; then
  for f in av_libglesv2.dll libHarfBuzzSharp.dll libSkiaSharp.dll spirv-cross.dll; do need "ValheimWorldEditor/$f"; done
  # ASP.NET Core's module for IIS: never loaded by the editor, one more native DLL for antivirus heuristics.
  if grep -qx ValheimWorldEditor/aspnetcorev2_inprocess.dll <<<"$list"; then echo "FORBIDDEN  ValheimWorldEditor/aspnetcorev2_inprocess.dll"; fail=1; fi
fi
bad=$(grep -E '\.cs$|\.csproj$|\.pdb$|/obj/|/bin/Release|wwwroot/|^ValheimWorldEditor/(models|maptex|terrain|game-look|plugin|export-game-files)/|heightmap\.frag\.glsl$|\.git/' <<<"$list" || true)
if [ -n "$bad" ]; then echo "FORBIDDEN in the package:"; echo "$bad" | head -20; fail=1; fi
size=$(du -m "$pkg" | cut -f1)
if [ "$size" -gt 150 ]; then echo "TOO BIG  ${size} MB"; fail=1; fi
[ $fail = 0 ] && echo "ok  $(basename "$pkg") (${size} MB, $(wc -l <<<"$list") entries)"
exit $fail
