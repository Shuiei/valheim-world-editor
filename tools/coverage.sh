#!/usr/bin/env bash
# Coverage of every test: which lines of the server (C#) and of the page's scripts the C# tests and
# the browser tests run. Prints both reports; the details are left in <out> (default /tmp/vwe-coverage):
#   server: coverage-merge.py over the two cobertura files, page: tests/browser/coverage.mjs --lines.
# The world generator (WorldGen/) is left out of the server's: measured, it is too slow for the tests.
# Needs the dotnet SDK, node with tests/browser's packages (npm ci), and coverlet.console (installed
# into <out>/tools on the first run).
set -euo pipefail
cd "$(dirname "$0")/.."
OUT=${1:-/tmp/vwe-coverage}
rm -rf "$OUT/build" "$OUT/cs" "$OUT/server" "$OUT/page"; mkdir -p "$OUT/server"
[ -x "$OUT/tools/coverlet" ] || dotnet tool install coverlet.console --tool-path "$OUT/tools" > /dev/null
echo "== C# tests"
dotnet test tests/WorldEditor.Tests --collect:"XPlat Code Coverage" --results-directory "$OUT/cs" 2>&1 | grep -E "Passed!|Failed!|error" || true
echo "== browser tests (server measured; a few minutes)"
dotnet build TerrainEditor.csproj -c Debug -o "$OUT/build" 2>&1 | grep -E " error " || true
# coverlet measures the files next to the one it is given (it takes that one for a test assembly).
(cd tests/browser && APP_DLL="$OUT/build/ValheimWorldEditor.dll" VWE_TIMEOUT=60000 VWE_COVERAGE="$OUT/page" \
  "$OUT/tools/coverlet" "$OUT/build/Photino.NET.dll" --target node \
  --targetargs "--test --test-concurrency=2 --test-reporter=spec $(echo *.test.mjs)" \
  --exclude-by-file "**/WorldGen/*.cs" -f cobertura -o "$OUT/server/" > "$OUT/browser.log" 2>&1) || true
grep -E "ℹ (tests|pass|fail)|^\\s*✖" "$OUT/browser.log" || true
echo "== server"
python3 tools/coverage-merge.py "$OUT/server/coverage.cobertura.xml" "$OUT"/cs/*/coverage.cobertura.xml
echo "== page"
(cd tests/browser && node coverage.mjs "$OUT/page" --lines > "$OUT/page.txt"; grep -v '^        ' "$OUT/page.txt")
