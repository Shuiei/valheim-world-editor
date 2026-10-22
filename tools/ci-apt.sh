#!/usr/bin/env bash
# Installs Debian packages in a CI job: ci-apt.sh [--no-install-recommends] <package>...
# A package mirror that stops answering leaves apt-get waiting with no output, until the job is killed after
# 10 minutes of silence (a whole test container lost): each try is cut after a few minutes and made again,
# three times, with apt's own retries and timeouts for each download.
set -uo pipefail
opts=(-qq -o Acquire::Retries=3 -o Acquire::http::Timeout=30 -o Acquire::https::Timeout=30)
for try in 1 2 3; do
  if timeout 240 apt-get "${opts[@]}" update && timeout 480 apt-get "${opts[@]}" install -y "$@" > /dev/null; then
    exit 0
  fi
  echo "apt-get did not finish (try $try of 3)" >&2
  sleep 10
done
exit 1
