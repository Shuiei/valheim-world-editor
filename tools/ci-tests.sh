#!/usr/bin/env bash
# The tests' side of CI's core-tests and app-tests jobs (.circleci/config.yml), for `circleci tests run`,
# which shares the test classes out between a job's containers by their last times and, on "Rerun
# failed tests", gives only the classes that failed.
# Usage: tools/ci-tests.sh list <project>                   the project's test classes, one per line
#        tools/ci-tests.sh run <project> <name> <class>...  runs those classes,
#                                                           results in /tmp/test-results/<name>/results.xml
#                                                           and, with VWE_COVERAGE=1 (CI on main), their
#                                                           coverage (Cobertura) as
#                                                           /tmp/coverage/<name>-<node>/coverage.<n>.cobertura.xml
# <project> is a test project (tests/Core.Tests/Core.Tests.csproj, tests/Desktop.Tests/Desktop.Tests.csproj).
# Needs the tests built (-c Release). Every test runs, so one run shows every failure; a test stuck 5
# minutes ends the run (with a dump). The visual tests (Category=Visual: the real editor, drawing with
# OpenGL) need a display: app-tests gives them one (Xvfb, Mesa's software OpenGL); without one they skip.
set -euo pipefail

case "${1:?list or run}" in
  list)
    project=${2:?test project}
    # Every test's full name (a theory's with its values in brackets), down to its class.
    dotnet test "$project" -c Release --no-build --list-tests | sed -n 's/^    //p' | sed 's/(.*//; s/\.[^.]*$//' | sort -u
    ;;
  run)
    project=${2:?test project}
    name=${3:?results name}
    shift 3
    # FullyQualifiedName~<class>. matches that class's tests only (a class name ends at the dot).
    filter=$(printf 'FullyQualifiedName~%s.|' "$@" | sed 's/|$//')
    # On main only, the coverage of this container's share, which the job sends to Codecov (merged
    # there with the other containers'): it makes the tests several times slower.
    coverage=()
    if [ "${VWE_COVERAGE:-}" = 1 ]; then
      coverage=(--settings tests/coverage.runsettings --collect "Code Coverage"
        --results-directory "/tmp/coverage/$name-${CIRCLE_NODE_INDEX:-0}")
    fi
    dotnet test "$project" -c Release --no-build --filter "$filter" --blame-hang-timeout 5m "${coverage[@]}" \
      --logger "junit;LogFilePath=/tmp/test-results/$name/results.xml" --logger "console;verbosity=normal"
    if [ "${VWE_COVERAGE:-}" = 1 ]; then
      # The report is named after the machine and the time (_<host>_<date>.cobertura.xml), which Codecov's
      # CLI does not take for coverage: renamed coverage.<n>.cobertura.xml (its *coverage*.* pattern),
      # numbered in case xargs runs this more than once in a container.
      out="/tmp/coverage/$name-${CIRCLE_NODE_INDEX:-0}"
      n=1
      find "$out" -name '*.cobertura.xml' ! -name 'coverage.*' | while read -r report; do
        # The first number free: a report already named is never replaced.
        while [ -e "$out/coverage.$n.cobertura.xml" ]; do
          n=$((n + 1))
        done
        mv "$report" "$out/coverage.$n.cobertura.xml"
      done
    fi
    ;;
  *)
    echo "usage: tools/ci-tests.sh list <project> | run <project> <name> <class>..." >&2
    exit 2
    ;;
esac
