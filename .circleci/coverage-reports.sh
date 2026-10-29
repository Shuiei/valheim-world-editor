#!/bin/bash
# Every batch's coverage report under a folder (/tmp/coverage on main: <suite>-<container>/<batch>/), one
# path a line, for the coverage job (.circleci/continue.yml). A passed test job has a report for every batch
# it ran (run-tests.sh fails one without), so at least one from each suite; none from a suite fails.
#   coverage-reports.sh <folder>
set -uo pipefail
root=$1
for suite in core app; do
  found=("$root"/"$suite"-*/*/coverage.cobertura.xml)
  [ -e "${found[0]}" ] || { echo "No coverage report from $suite-tests." >&2; exit 1; }
  echo "$suite-tests: ${#found[@]} batch reports" >&2
  printf '%s\n' "${found[@]}"
done
