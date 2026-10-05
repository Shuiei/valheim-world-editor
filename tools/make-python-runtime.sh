#!/usr/bin/env bash
# A small Python runtime with the export tool's packages, shipped in the release so users install
# nothing: a stripped CPython build from python-build-standalone, the packages from
# tools/asset-export/requirements.txt, and nothing the exporter does not use (Tk, tests, pip...).
# Usage: tools/make-python-runtime.sh <linux-x64|win-x64> <folder>   (makes <folder>/python; needs curl, python3)
set -euo pipefail
rid=${1:?linux-x64 or win-x64}; out=$(realpath -m "${2:?output folder}")
repo=$(cd "$(dirname "$0")/.." && pwd)
pbs=20261003 ver=3.12.15
case "$rid" in
  linux-x64) triple=x86_64-unknown-linux-gnu ;;
  win-x64) triple=x86_64-pc-windows-msvc ;;
  *) echo "unknown runtime $rid" >&2; exit 1 ;;
esac
cache=${PY_CACHE:-$HOME/.cache/vwe-python}; mkdir -p "$cache"
tgz="$cache/cpython-$ver+$pbs-$triple-install_only_stripped.tar.gz"
[ -f "$tgz" ] || curl -fsSL -o "$tgz" "https://github.com/astral-sh/python-build-standalone/releases/download/$pbs/$(basename "$tgz")"
rm -rf "$out/python"; mkdir -p "$out"; tar -xzf "$tgz" -C "$out"
py="$out/python"
shopt -s nullglob
if [ "$rid" = linux-x64 ]; then
  "$py/bin/python3" -m pip install -q --no-warn-script-location --disable-pip-version-check -r "$repo/tools/asset-export/requirements.txt"
  site="$py/lib/python3.12/site-packages"; lib="$py/lib/python3.12"
  # python3.12 is linked statically; the shared library and the tools are not needed.
  rm -rf "$py/include" "$py/share" "$py/lib/pkgconfig" "$py/lib/libpython3"* "$py/lib/libtcl"* "$py/lib/libtk"* "$py/lib/tcl"* "$py/lib/tk"* \
    "$py/lib/itcl"* "$py/lib/thread"* "$lib"/config-* "$lib"/lib-dynload/_tkinter*
  (cd "$py/bin" && rm -f 2to3* idle3* pip* pydoc3* python3*-config archspec)
  # Zip files (Thunderstore) cannot hold the python and python3 links: the editor runs python3.12.
  rm -f "$py/bin/python" "$py/bin/python3"
else
  # Windows packages come as ready-made wheels; install them from here into the Windows tree.
  site="$py/Lib/site-packages"; lib="$py/Lib"
  python3 -m pip install -q --disable-pip-version-check --target "$site" --platform win_amd64 --python-version 3.12 \
    --implementation cp --only-binary=:all: -r "$repo/tools/asset-export/requirements.txt"
  rm -rf "$py/include" "$py/libs" "$py/tcl" "$py/DLLs"/tcl* "$py/DLLs"/tk* "$py/DLLs/_tkinter.pyd" "$site/bin"
fi
rm -rf "$site"/pip "$site"/pip-*.dist-info "$lib"/{idlelib,ensurepip,tkinter,turtledemo,test,lib2to3,pydoc_data}
find "$py" -name __pycache__ -type d -prune -exec rm -rf {} +
find "$py" -name '*.a' -delete
du -sh "$py"
