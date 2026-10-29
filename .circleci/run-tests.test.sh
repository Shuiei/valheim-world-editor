#!/bin/bash
# The test jobs' scripts checked against a stand-in dotnet (the code-analysis job runs this):
#   list-classes.sh     the suites' discover: test names cut down to their classes, once each
#   run-tests.sh        a batch: every class a --filter-class, the results where the CLI asks; a failed test's
#                       status kept with its results; a crashed or stuck test process as one failed test of
#                       its class, in well-formed XML; an empty batch runs nothing; on main, the coverage in
#                       <suite>-<container>/<batch>/, and a passed batch without it fails
#   coverage-reports.sh every batch's report of both suites, and none from a suite fails
#   test-suites.yml     both suites keep their results and share one queue
# Run it from the repository's root: bash .circleci/run-tests.test.sh
# Each check takes the status of the test (often a condition) run just before it.
# shellcheck disable=SC2319
set -uo pipefail
here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

fail() { echo "FAIL: $*"; failures=$((failures + 1)); }
# A check's name and the status of its test, run just before: check "what it checks" $?
check() { if [ "$2" = 0 ]; then echo "ok: $1"; else fail "$1"; fi; }

# The stand-in dotnet: --list-tests prints a listing as dotnet test does; a run records its arguments and, as
# FAKE says, writes the results (pass, fail), none (crash: the process's error output; hang: the hang dump's
# log), or the results without the coverage report (nocov).
mkdir -p "$work/bin"
cat > "$work/bin/dotnet" <<'EOF'
#!/bin/bash
printf '%s\n' "$@" > "$FAKE_ARGS"
if printf '%s\n' "$@" | grep -qx -- --list-tests; then
  cat <<'LIST'
Discovering tests...
The following Tests are available:
  WorldEditor.Tests.SaveTests.RoundTrip
  WorldEditor.Tests.SaveTests.KeepsZones(count: 3)
  WorldEditor.Tests.SaveTests.KeepsZones(count: 4)
  WorldEditor.Tests.Outer+InnerTests.Works
  WorldEditor.Tests.ZoneTests.Edges
LIST
  exit 0
fi
args=("$@")
value() { local i; for ((i = 0; i < ${#args[@]} - 1; i++)); do [ "${args[i]}" = "$1" ] && { printf '%s' "${args[i + 1]}"; return; }; done; }
junit=$(value --report-spekt-junit-filename)
runs=$(value --results-directory)
cover=$(value --coverage-output)
results() { mkdir -p "$(dirname "$junit")"; printf '<?xml version="1.0" encoding="utf-8"?>\n<testsuites><testsuite name="x" tests="1" failures="%s"/></testsuites>\n' "$1" > "$junit"; }
case "$FAKE" in
  pass) results 0; [ -z "$cover" ] || { mkdir -p "$(dirname "$cover")"; echo '<coverage/>' > "$cover"; }; exit 0 ;;
  nocov) results 0; exit 0 ;;
  fail) results 1; exit 2 ;;
  crash)
    printf '%s\n' 'Exit code: 134' '  Error output: ' 'Unhandled exception. System.InvalidOperationException: boom & <bad>' \
      '   at WorldEditor.Tests.CrashTests+<Explodes>d__4.MoveNext()' "$(printf 'a control byte: \001')"
    exit 134 ;;
  hang) mkdir -p "$runs"; echo '[00:05:00] WorldEditor.Tests.HungTests.Waits(seconds: 9)' > "$runs/x_hang.log"; exit 3 ;;
esac
EOF
chmod +x "$work/bin/dotnet"
export PATH="$work/bin:$PATH" FAKE_ARGS="$work/args" VWE_TEST_RUNS="$work/runs" VWE_COVERAGE_ROOT="$work/coverage"
unset CIRCLE_BRANCH CIRCLE_NODE_INDEX

well_formed() {
  if command -v xmllint > /dev/null; then xmllint --noout "$1" 2> /dev/null
  else python3 -c 'import sys, xml.dom.minidom; xml.dom.minidom.parse(sys.argv[1])' "$1" 2> /dev/null; fi
}

# The suites' discover: classes once each, a theory's values and the method cut, a nested class kept.
classes=$(bash "$here/list-classes.sh" editor/tests/Core.Tests/Core.Tests.csproj)
[ "$classes" = "$(printf "%s\n" "WorldEditor.Tests.Outer+InnerTests" WorldEditor.Tests.SaveTests WorldEditor.Tests.ZoneTests)" ]
check "discover lists each class once" $?

# A batch that passes: each class a --filter-class, the results where the CLI asks, no coverage off main.
junit=$work/out/pass.xml
FAKE=pass bash "$here/run-tests.sh" core editor/tests/Core.Tests/Core.Tests.csproj "$junit" <<< $'WorldEditor.Tests.SaveTests\nWorldEditor.Tests.ZoneTests\n' > /dev/null
rc=$?
[ "$rc" = 0 ]
check "a passing batch succeeds" $?
grep -qx -- --filter-class=WorldEditor.Tests.SaveTests "$FAKE_ARGS" && grep -qx -- --filter-class=WorldEditor.Tests.ZoneTests "$FAKE_ARGS"
check "each class is a --filter-class" $?
[ -s "$junit" ]
check "the results go where the CLI asks" $?
! grep -qx -- --coverage "$FAKE_ARGS"
check "no coverage off main" $?

# A batch with a failed test: the job fails, and the test's own results stay (not a crash's).
junit=$work/out/fail.xml
FAKE=fail bash "$here/run-tests.sh" core p "$junit" <<< 'WorldEditor.Tests.SaveTests' > /dev/null
rc=$?
[ "$rc" != 0 ]
check "a failed test fails the batch" $?
grep -q "failures=\"1\"" "$junit" && ! grep -q "stuck\|crashed" "$junit"
check "a failed test keeps its own results" $?

# A crashed test process: one failed test of the class on the crash's stack, in well-formed XML.
junit=$work/out/crash.xml
FAKE=crash bash "$here/run-tests.sh" app p "$junit" <<< 'WorldEditor.Tests.CrashTests' > /dev/null
rc=$?
[ "$rc" != 0 ]
check "a crash fails the batch" $?
well_formed "$junit"
check "a crash is reported as well-formed XML" $?
grep -q "classname=\"WorldEditor.Tests.CrashTests\"" "$junit" && grep -q "crashed (Exit code: 134)" "$junit"
check "a crash names the class on its stack" $?

# A stuck test: one failed test of the stuck test's class.
junit=$work/out/hang.xml
FAKE=hang bash "$here/run-tests.sh" app p "$junit" <<< 'WorldEditor.Tests.HungTests' > /dev/null
rc=$?
[ "$rc" != 0 ]
check "a stuck test fails the batch" $?
well_formed "$junit" && grep -q "classname=\"WorldEditor.Tests.HungTests\"" "$junit" && grep -q "was stuck" "$junit"
check "a stuck test is reported as well-formed XML, with its class" $?

# An empty batch: dotnet test is not run (it would run every class), and an empty result is written.
rm -f "$FAKE_ARGS"
junit=$work/out/empty.xml
FAKE=pass bash "$here/run-tests.sh" core p "$junit" <<< $'\n  \n' > /dev/null
rc=$?
[ "$rc" = 0 ] && [ ! -e "$FAKE_ARGS" ]
check "an empty batch succeeds without running dotnet" $?
well_formed "$junit" && grep -q "<testsuites />" "$junit"
check "an empty batch writes an empty result" $?

# On main: the coverage in <suite>-<container>/<batch>/; a passed batch without its report fails.
export CIRCLE_BRANCH=main CIRCLE_NODE_INDEX=2
FAKE=pass bash "$here/run-tests.sh" core p "$work/out/main.xml" <<< 'WorldEditor.Tests.SaveTests' > /dev/null
rc=$?
[ "$rc" = 0 ]
check "a main batch with its coverage succeeds" $?
ls "$VWE_COVERAGE_ROOT"/core-2/*/coverage.cobertura.xml > /dev/null 2>&1
check "the coverage goes to <suite>-<container>/<batch>/" $?
FAKE=nocov bash "$here/run-tests.sh" core p "$work/out/nocov.xml" <<< 'WorldEditor.Tests.SaveTests' > /dev/null
rc=$?
[ "$rc" != 0 ]
check "a main batch without its coverage fails" $?
unset CIRCLE_BRANCH CIRCLE_NODE_INDEX

# The coverage job: every batch's report of both suites, whatever the containers' count; none from one fails.
root=$work/merge
mkdir -p "$root/core-0/1" "$root/core-2/7" "$root/app-5/3"
for d in core-0/1 core-2/7 app-5/3; do echo '<coverage/>' > "$root/$d/coverage.cobertura.xml"; done
reports=$(bash "$here/coverage-reports.sh" "$root" 2> /dev/null)
rc=$?
[ "$rc" = 0 ] && [ "$(printf "%s\n" "$reports" | wc -l)" = 3 ]
check "every batch's report is found" $?
rm -r "$root/app-5"
bash "$here/coverage-reports.sh" "$root" > /dev/null 2>&1
rc=$?
[ "$rc" != 0 ]
check "a suite without a report fails" $?

# Both suites keep their results and share one queue.
for suite in "core tests" "app tests"; do
  block=$(awk -v name="name: $suite" '$0 == name { on = 1; next } /^---/ { on = 0 } on' "$here/test-suites.yml")
  grep -q "store-test-results: true" <<< "$block" && grep -q "dynamic-test-splitting: true" <<< "$block"
  check "$suite keep their results and share one queue" $?
done

if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed."
  exit 1
fi
echo "All checks passed."
