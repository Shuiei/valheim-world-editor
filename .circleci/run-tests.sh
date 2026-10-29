#!/bin/bash
# One batch of a test project's classes, for CircleCI's testsuite CLI (.circleci/test-suites.yml):
#   run-tests.sh <suite> <project> <junit file>
# The classes come on stdin, one full name a line (as the suite's discover lists them); each becomes a
# --filter-class of dotnet test (an empty batch runs nothing and writes an empty result), and the
# results (JUnit) go where the CLI asks. Every test runs,
# failing or not; a test stuck 5 minutes ends the run with a dump (editor/tests/testconfig.json; 10 minutes
# under coverage). On main only, the batch's coverage is kept for the coverage job
# (.circleci/continue.yml) in /tmp/coverage/<suite>-<container>/<batch>/ ($VWE_COVERAGE_ROOT, for
# .circleci/run-tests.test.sh, as $VWE_TEST_RUNS stands for /tmp/test-runs). A test process that
# crashed, or was stopped stuck, writes no results: one failed test in their place, so CircleCI shows
# it and "Rerun failed tests" runs its class again.
set -uo pipefail
suite=$1 project=$2 junit=$3
batch=$(date +%s%N)
# The run's own files (a stuck test's dump: hundreds of MB), out of the artifacts; its output in
# output.log, for the crash report.
runs=${VWE_TEST_RUNS:-/tmp/test-runs}/$suite-$batch
mkdir -p "$runs"
log=$runs/output.log

# On main only, the coverage of this batch: it makes the tests several times slower, so a test is stuck
# after 10 minutes there (the world generator's tests share the container's two cores with the others
# for minutes; one took 300 s on main). VWE_COVERAGE tells the tests so: the editor's pictures may wait
# longer (EditorProcess), speeds are not checked (SeedPreviewTests).
coverage=()
report=""
if [ "${CIRCLE_BRANCH:-}" = main ]; then
  export VWE_COVERAGE=1
  report=${VWE_COVERAGE_ROOT:-/tmp/coverage}/$suite-${CIRCLE_NODE_INDEX:-0}/$batch/coverage.cobertura.xml
  coverage=(--coverage --coverage-output-format cobertura --coverage-settings "$PWD/editor/tests/coverage.runsettings"
    --coverage-output "$report" --hangdump-timeout 10m)
fi

mapfile -t classes < <(grep -v '^[[:space:]]*$')
if [ "${#classes[@]}" -eq 0 ]; then
  # An empty batch (a container left with nothing to run): dotnet test with no --filter-class would run
  # every class of the project again here. No tests, an empty result.
  echo "Empty batch: no test classes to run."
  mkdir -p "$(dirname "$junit")"
  printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>' '<testsuites />' > "$junit"
  exit 0
fi

dotnet test --project "$project" -c Release --no-build --no-ansi --no-progress --output Detailed \
    --results-directory "$runs" "${coverage[@]}" --report-spekt-junit --report-spekt-junit-filename "$junit" \
    "${classes[@]/#/--filter-class=}" \
  2>&1 | tee "$log"
rc=${PIPESTATUS[0]}

if [ ! -e "$junit" ]; then
  # No results: the test process crashed or was stopped stuck. One failed test in their place; its class
  # is the stuck test's (the hang dump's log: "[00:05:00] WorldEditor.Tests.SomeTests.AMethod"), else the
  # test class on the crash's stack (in the process's error output:
  # "   at WorldEditor.Tests.SomeTests+<AMethod>d__4.MoveNext()": the nested part stands in for a
  # method), else TestHost; its text, the end of the run's output.
  stuck=$(cat "$runs"/*_hang.log 2> /dev/null || true)
  if [ -n "$stuck" ]; then
    what="A test was stuck: the test process was stopped (with a dump)"
    class=$(printf '%s\n' "$stuck" | sed -n -E '1s/^\[[0-9:.]+\] +//; 1s/\(.*//; 1s/\.[A-Za-z0-9_]+$//p')
  else
    what="The test process crashed ($(grep -m 1 -o '^Exit code: [0-9]*' "$log" || echo 'no exit code'))"
    class=$(sed -n '/^  Error output: /,$p' "$log" \
      | grep -E -o '^ +at (TerrainEditor\.Desktop\.Tests|WorldEditor\.Tests)\.[A-Za-z0-9_.+<>]+' \
      | head -n 1 | sed -E 's/^ +at //; s/\+.*/.x/; s/\.[A-Za-z0-9_]+$//' || true)
  fi
  # Well-formed XML: no control bytes (not even in CDATA), the class (a generic one has < >) escaped.
  report_text=$( { printf '%s\n' "$stuck"; tail -n 80 "$log"; } \
    | LC_ALL=C tr -d '\000-\010\013\014\016-\037' | sed 's/]]>/]]]]><![CDATA[>/g')
  class=$(printf '%s' "${class:-TestHost}" | LC_ALL=C tr -d '\000-\037' \
    | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g' -e 's/"/\&quot;/g')
  mkdir -p "$(dirname "$junit")"
  cat > "$junit" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<testsuites>
  <testsuite name="$class" tests="1" failures="1" errors="0">
    <testcase classname="$class" name="$what" time="0">
      <failure message="$what: the tests still running were not finished."><![CDATA[$report_text]]></failure>
    </testcase>
  </testsuite>
</testsuites>
EOF
  [ "$rc" != 0 ] || rc=1
elif [ "$rc" = 0 ] && [ -n "$report" ] && [ ! -e "$report" ]; then
  # Only a batch that passed needs its report: a failed one fails the job, so the coverage job does not run.
  echo "No coverage report for this batch: Codecov would get nothing from it."
  rc=1
fi
exit "$rc"
