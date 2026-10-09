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

printf '%s\n' "$out" | python3 -c '
import json, sys
lines = [l.rstrip("\n") for l in sys.stdin if l.startswith("@@ ")]
if "@@ ready" not in lines:
    sys.exit("smoke test: the editor never got ready")
errors = [l for l in lines if l.startswith("@@ error")]
if errors:
    sys.exit("smoke test: " + errors[0])
oks = [json.loads(l[len("@@ ok "):]) for l in lines if l.startswith("@@ ok ")]
if not oks:
    sys.exit("smoke test: no answer from the editor")
s = oks[-1]
page, frames, gl, objects = s.get("page"), s.get("frames", 0), s.get("glErrors", 0), s.get("objects", 0)
problems = []
if page != "editor": problems.append(f"page is {page!r}, not the 3D editor")
if not frames: problems.append("no frame drawn")
if gl: problems.append(f"{gl} OpenGL error(s)")
if problems:
    sys.exit("smoke test: " + "; ".join(problems))
print(f"smoke test: ok ({frames} frames, {objects} objects, no OpenGL error)")
''' || { show_why; exit 1; }
