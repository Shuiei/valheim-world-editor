#!/usr/bin/env bash
# Checks a release package: what must be in it, and what must never be (source code, debug files,
# files extracted from the game, the plugin: it is released on its own).
# Usage: tools/check-package.sh <package.tar.gz|.zip>
set -euo pipefail
pkg=${1:?package}
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
fi
bad=$(grep -E '\.cs$|\.csproj$|\.pdb$|/obj/|/bin/Release|wwwroot/|^ValheimWorldEditor/(models|maptex|terrain|game-look|plugin|export-game-files)/|heightmap\.frag\.glsl$|\.git/' <<<"$list" || true)
if [ -n "$bad" ]; then echo "FORBIDDEN in the package:"; echo "$bad" | head -20; fail=1; fi
size=$(du -m "$pkg" | cut -f1)
if [ "$size" -gt 150 ]; then echo "TOO BIG  ${size} MB"; fail=1; fi
[ $fail = 0 ] && echo "ok  $(basename "$pkg") (${size} MB, $(wc -l <<<"$list") entries)"
exit $fail
