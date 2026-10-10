#!/usr/bin/env bash
# Checks a release package: what must be in it, and what must never be (source code, debug files,
# files extracted from the game, the plugin: it is released on its own).
# Usage: tools/check-package.sh <package.tar.gz|.zip>
set -euo pipefail
pkg=${1:?package}
case "$pkg" in
  *.tar.gz) list=$(tar -tzf "$pkg"); exe=ValheimWorldEditor/ValheimWorldEditor ;;
  *.zip) list=$(unzip -Z1 "$pkg"); exe=ValheimWorldEditor/ValheimWorldEditor.exe ;;
  *) echo "unknown package type: $pkg" >&2; exit 2 ;;
esac
fail=0
need() { grep -qx "$1" <<<"$list" || { echo "MISSING  $1"; fail=1; }; }
for f in "$exe" ValheimWorldEditor/README.txt; do need "$f"; done
bad=$(grep -E '\.cs$|\.csproj$|\.pdb$|/obj/|/bin/Release|wwwroot/|^ValheimWorldEditor/(models|maptex|terrain|game-look|plugin|export-game-files)/|heightmap\.frag\.glsl$|\.git/' <<<"$list" || true)
if [ -n "$bad" ]; then echo "FORBIDDEN in the package:"; echo "$bad" | head -20; fail=1; fi
size=$(du -m "$pkg" | cut -f1)
if [ "$size" -gt 150 ]; then echo "TOO BIG  ${size} MB"; fail=1; fi
[ $fail = 0 ] && echo "ok  $(basename "$pkg") (${size} MB, $(wc -l <<<"$list") entries)"
exit $fail
