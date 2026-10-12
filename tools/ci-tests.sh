#!/usr/bin/env bash
# The tests' side of CI's core-tests and app-tests jobs (.circleci/config.yml), for `circleci tests run`,
# which shares the test classes out between a job's containers by their last times and, on "Rerun
# failed tests", gives only the classes that failed.
# Usage: tools/ci-tests.sh list <project>                   the project's test classes, one per line
#        tools/ci-tests.sh run <project> <name> <class>...  runs those classes,
#                                                           results in /tmp/test-results/<name>/results.xml
#                                                           (and crash-*.xml when the test process crashes)
#                                                           and, with VWE_COVERAGE=1 (CI on main), their
#                                                           coverage (Cobertura) as
#                                                           /tmp/coverage/<name>-<node>/coverage.<n>.cobertura.xml
# <project> is a test project (tests/Core.Tests/Core.Tests.csproj, tests/Desktop.Tests/Desktop.Tests.csproj).
# Needs the tests built (-c Release). Every test runs, so one run shows every failure; a test stuck 5
# minutes ends the run (with a dump). The visual tests (Category=Visual: the real editor, drawing with
# OpenGL) need a display: app-tests gives them one (Xvfb, Mesa's software OpenGL); without one they skip.
set -euo pipefail

# A crashed test process as a failed test in JUnit (crash-<n>.xml in <folder>), from the run's output <log>:
# the runtime's report (from "Fatal error.") as its text, and the test class on its stack as its class, so
# CircleCI shows it among the failures and "Rerun failed tests" runs that class again ("TestHost" when the
# crash was on a thread of no test).
crash_result() {
  local log=$1 folder=$2 code report class
  code=$(grep -o 'crashed with exit code [0-9]*' "$log" | head -n 1 | grep -o '[0-9]*$')
  report=$(sed -n '/^Fatal error\./,/^\[xUnit\.net/p' "$log" | head -n 60)
  # "   at WorldEditor.Tests.SomeTests.AMethod()" or "...SomeTests+<AMethod>d__4.MoveNext()": SomeTests.
  class=$(printf '%s\n' "$report" | grep -E -o '^ +at (TerrainEditor\.Desktop\.Tests|WorldEditor\.Tests)\.[A-Za-z0-9_.+<>]+' \
    | head -n 1 | sed -E 's/^ +at //; s/\+.*//; s/\.[A-Za-z0-9_]+$//' || true)
  [ -n "$class" ] || class=TestHost
  mkdir -p "$folder"
  {
    printf '<?xml version="1.0" encoding="utf-8"?>\n<testsuites>\n'
    printf '  <testsuite name="%s" tests="1" failures="1" errors="0">\n' "$class"
    printf '    <testcase classname="%s" name="The test process crashed (exit code %s)" time="0">\n' "$class" "${code:-?}"
    printf '      <failure message="The test process crashed (exit code %s): the tests still running were not finished."><![CDATA[%s]]></failure>\n' \
      "${code:-?}" "${report//]]>/]]]]><![CDATA[>}"
    printf '    </testcase>\n  </testsuite>\n</testsuites>\n'
  } > "$(mktemp "$folder/crash-XXXXXX.xml")"
}

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
    # The run's output is kept too: a test process that crashes leaves no failed test in the results (only
    # the tests done before it), so the crash is written there as one (crash_result).
    log=$(mktemp)
    status=0
    dotnet test "$project" -c Release --no-build --filter "$filter" --blame-hang-timeout 5m "${coverage[@]}" \
      --logger "junit;LogFilePath=/tmp/test-results/$name/results.xml" --logger "console;verbosity=normal" 2>&1 \
      | tee "$log" || status=$?
    if grep -q 'Test process crashed with exit code' "$log"; then
      crash_result "$log" "/tmp/test-results/$name"
    fi
    rm -f "$log"
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
    exit "$status"
    ;;
  *)
    echo "usage: tools/ci-tests.sh list <project> | run <project> <name> <class>..." >&2
    exit 2
    ;;
esac
