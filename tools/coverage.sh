#!/usr/bin/env bash
# CI's coverage job (.circleci/config.yml): merges the Cobertura files the test containers wrote
# (tools/ci-tests.sh) into one report, and fails when the editor's line coverage is under the minimum.
# Usage: tools/coverage.sh <coverage dir> <report dir> <minimum %> [<container>...]
# Each <container> (core-0, app-2...: tools/ci-tests.sh's <name>-<node>) must have written its
# coverage, so the minimum is never checked against only part of the tests.
# Needs the repository's .NET tools (dotnet tool restore: ReportGenerator).
set -euo pipefail
input=${1:?coverage dir}
report=${2:?report dir}
minimum=${3:?minimum line coverage, in %}
shift 3

for container in "$@"; do
  if [ -z "$(find "$input/$container" -name '*.cobertura.xml' 2>/dev/null)" ]; then
    echo "No coverage from the $container test container (under $input/$container)" >&2
    exit 1
  fi
done

files=$(find "$input" -name '*.cobertura.xml' | sort)
if [ -z "$files" ]; then
  echo "No coverage file under $input" >&2
  exit 1
fi
echo "Merging $(echo "$files" | wc -l) coverage files"

# The HTML report (CI's artifacts) and a text summary, with each class's coverage.
dotnet tool run reportgenerator "-reports:$(echo "$files" | paste -sd ';')" "-targetdir:$report" \
  "-reporttypes:Html;TextSummary" -verbosity:Warning
cat "$report/Summary.txt"

line=$(sed -n 's/^ *Line coverage: *\([0-9.]*\)%.*/\1/p' "$report/Summary.txt" | head -1)
if [ -z "$line" ]; then
  echo "No line coverage in $report/Summary.txt" >&2
  exit 1
fi
if awk -v l="$line" -v m="$minimum" 'BEGIN { exit !(l < m) }'; then
  echo "Line coverage $line% is under the minimum of $minimum%" >&2
  exit 1
fi
echo "Line coverage $line% (minimum $minimum%)"
