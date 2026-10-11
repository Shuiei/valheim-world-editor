#!/usr/bin/env bash
# Starts a packaged editor the way a player does (the program file from the release package) and
# checks it really works, through its test driver: it starts, loads SPIRV-Cross (spirv), opens the
# test world, opens an area in the 3D editor, and draws frames with no OpenGL error. CI runs it on the Linux package (under Xvfb,
# with Mesa's software OpenGL); tools/smoke-test.ps1 does the same with the Windows one.
# Usage: tools/smoke-test.sh <program file> <world folder>
set -euo pipefail
prog=$(realpath "${1:?program file}")
world=$(realpath "${2:?world folder}")
home=$(mktemp -d); trap 'rm -rf "$home"' EXIT

# A home of its own: no settings, worlds or game look of this computer's (plain colours then).
out=$( { printf 'spirv\nworld %s\narea 0 0 1\nwait 2000\nstate\nquit\n' "$world"; } \
  | HOME="$home" XDG_DATA_HOME="$home/.local/share" XDG_CONFIG_HOME="$home/.config" timeout 180 "$prog" --driver 2>&1 ) || true
printf '%s\n' "$out" | grep '^@@' | cut -c1-200
# When it fails: what the app said besides the driver, and its log (which says how it draws).
show_why() {
  echo "--- the app's other output:"; printf '%s\n' "$out" | grep -v '^@@' | tail -40
  echo "--- its log:"; tail -40 "$home/.local/share/ValheimWorldEditor/ValheimWorldEditor.log" 2>/dev/null || echo "(none)"
}

# The driver's answers: it got ready, said no error, and its last state shows the 3D editor drawing
# frames with no OpenGL error. The state is one line of JSON; its few numbers are read with sed.
answers=$(printf '%s\n' "$out" | grep '^@@ ' || true)
fail() { echo "smoke test: $1"; show_why; exit 1; }
grep -qx '@@ ready' <<< "$answers" || fail "the editor never got ready"
err=$(grep -m1 '^@@ error' <<< "$answers" || true)
[[ -z $err ]] || fail "$err"
state=$(grep '^@@ ok ' <<< "$answers" | tail -1)
[[ -n $state ]] || fail "no answer from the editor"
field() { sed -nE "s/.*\"$1\":(\"([^\"]*)\"|([0-9]+)).*/\\2\\3/p" <<< "$state"; }
page=$(field page); frames=$(field frames); gl=$(field glErrors); objects=$(field objects)
problems=()
[[ $page == editor ]] || problems+=("page is '$page', not the 3D editor")
(( ${frames:-0} > 0 )) || problems+=("no frame drawn")
(( ${gl:-0} == 0 )) || problems+=("$gl OpenGL error(s)")
if (( ${#problems[@]} )); then
  joined=$(printf '; %s' "${problems[@]}")
  fail "${joined:2}"
fi
echo "smoke test: ok ($frames frames, ${objects:-0} objects, no OpenGL error)"
