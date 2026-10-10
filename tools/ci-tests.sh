#!/usr/bin/env bash
# CI's native-tests job runs on several containers (parallelism in .circleci/config.yml): each runs its
# share of the test classes, split by the time each class took in the last runs (CircleCI keeps them
# from the stored test results; a class without a time is shared out by name).
# Usage: tools/ci-tests.sh share            this container's classes, into /tmp/test-share.txt
#        tools/ci-tests.sh run <ns> <name>  runs this container's classes of namespace <ns> (no visual
#                                           tests), results in /tmp/test-results/<name>/results.xml
# Needs the tests built (-c Release). Fail fast: xUnit stops at the first failure (StopOnFail), a test
# stuck 5 minutes ends the run (with a dump).
set -euo pipefail
project=tests/Desktop.Tests/Desktop.Tests.csproj
share=/tmp/test-share.txt

case "${1:?share or run}" in
  share)
    # Every test's full name (a theory's with its values in brackets), down to its class.
    dotnet test "$project" -c Release --no-build --list-tests | sed -n 's/^    //p' | sed 's/(.*//; s/\.[^.]*$//' | sort -u > /tmp/test-classes.txt
    circleci tests split --split-by=timings --timings-type=classname /tmp/test-classes.txt > "$share"
    echo "$(wc -l < "$share") of $(wc -l < /tmp/test-classes.txt) test classes here:"
    cat "$share"
    ;;
  run)
    ns=${2:?namespace} name=${3:?results name}
    classes=$(grep "^$ns\." "$share" || true)
    if [ -z "$classes" ]; then
      echo "no $ns classes in this container's share"
      exit 0
    fi
    # FullyQualifiedName~<class>. matches that class's tests only (a class name ends at the dot).
    filter="Category!=Visual&($(echo "$classes" | sed 's/.*/FullyQualifiedName~&./' | paste -sd '|'))"
    dotnet test "$project" -c Release --no-build --filter "$filter" --blame-hang-timeout 5m \
      --logger "junit;LogFilePath=/tmp/test-results/$name/results.xml" --logger "console;verbosity=normal" \
      -- xUnit.StopOnFail=true
    ;;
  *)
    echo "usage: tools/ci-tests.sh share | run <namespace> <name>" >&2
    exit 2
    ;;
esac
