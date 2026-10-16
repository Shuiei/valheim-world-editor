#!/usr/bin/env bash
# Checks the formatting of the editor's projects, as .editorconfig says (dotnet format whitespace and
# style, verifying only): the projects given, else all five. With --since <ref>, only the projects
# that changed since <ref> (since its merge base with HEAD): one changes when a file under its folder
# does, and all of them when a shared configuration file does (.editorconfig, Directory.Build.props,
# global.json, this script). CI's formatting job runs it on every project on main and with
# --since origin/main on branches and pull requests, the projects shared out between its containers.
# dotnet format checks nothing, and still exits 0, for a project that is not restored ("Required
# references did not load"): each project is restored first, and that message fails the check. Its
# "Warnings were encountered while loading the workspace" is the referenced projects' missing build
# output (no metadata reference, so the project reference is used): harmless (dotnet format -v diag).
# Usage: tools/format-check.sh [--since <ref>] [<project.csproj>...]
set -euo pipefail
cd "$(dirname "$0")/.."

all=(Core/ValheimWorldEditor.Core.csproj Desktop/ValheimWorldEditor.Desktop.csproj
  Sdk/ValheimWorldEditor.Sdk.csproj plugin/WorldEditorBridge.Api/WorldEditorBridge.Api.csproj
  samples/ExampleVehicles.WorldEditor/ExampleVehicles.WorldEditor.csproj)
since=
if [ "${1:-}" = --since ]; then since=${2:?a ref after --since}; shift 2; fi
projects=("$@")
[ ${#projects[@]} -gt 0 ] || projects=("${all[@]}")

if [ -n "$since" ]; then
  git rev-parse --verify -q "$since^{commit}" > /dev/null || { echo "No such ref: $since (git fetch origin main?)" >&2; exit 2; }
  changed=$(git diff --name-only "$(git merge-base "$since" HEAD)" HEAD)
  if printf '%s\n' "$changed" | grep -q -x -e .editorconfig -e Directory.Build.props -e global.json -e tools/format-check.sh; then
    echo "A shared configuration file changed since $since: every project is checked."
  else
    kept=()
    for p in "${projects[@]}"; do
      if printf '%s\n' "$changed" | grep -q "^$(dirname "$p")/"; then kept+=("$p"); else echo "Unchanged since $since, not checked: $p"; fi
    done
    projects=(${kept[@]+"${kept[@]}"})
  fi
fi

log=$(mktemp); trap 'rm -f "$log"' EXIT
for p in ${projects[@]+"${projects[@]}"}; do
  echo "== $p"
  dotnet restore "$p" -v quiet
  for check in whitespace style; do
    dotnet format "$check" "$p" --verify-no-changes --no-restore 2>&1 | tee "$log"
    if grep -q "Required references did not load" "$log"; then
      echo "dotnet format $check checked nothing in $p: its references did not load (restore failed?)." >&2
      exit 1
    fi
  done
done
