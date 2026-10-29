#!/bin/bash
# A test project's classes, one full name a line (a nested one as Outer+Inner), for the suites' discover
# (.circleci/test-suites.yml): dotnet test --list-tests prints each test's full name, indented, a theory's
# with its values in brackets; each is cut down to its class, once.
#   list-classes.sh <project>
set -euo pipefail
dotnet test --project "$1" -c Release --no-build --no-ansi --no-progress --list-tests \
  | sed -n 's/^  \([A-Za-z_]\)/\1/p' | sed 's/(.*//; s/\.[^.]*$//' | sort -u
