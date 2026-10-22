#!/usr/bin/env bash
# Installs Debian packages in a CI job: ci-apt.sh [--no-install-recommends] <package>...
# A package mirror that stops answering leaves apt-get waiting with no output, until CircleCI kills the step after
# its minutes of silence (10 for most steps, 5 for some), and a whole container is lost. So each try is cut after a
# few minutes and made again, three times, with apt's own retries and timeouts for each download; and a line is
# written every minute while apt-get works, so that a slow mirror is waited for instead of taken for a stuck step.
set -uo pipefail
opts=(-qq -o Acquire::Retries=3 -o Acquire::http::Timeout=30 -o Acquire::https::Timeout=30)
( while sleep 60; do echo "apt-get still working…"; done ) &
beat=$!
trap 'pkill -P "$beat" 2> /dev/null; kill "$beat" 2> /dev/null' EXIT
for try in 1 2 3; do
  echo "Installing $* (try $try of 3)"
  if timeout 180 apt-get "${opts[@]}" update && timeout 360 apt-get "${opts[@]}" install -y "$@" > /dev/null; then
    exit 0
  fi
  echo "apt-get did not finish (try $try of 3)" >&2
  sleep 10
done
exit 1
