#!/usr/bin/env bash
# Runs the EditMode unit tests (unity/Assets/Tests/EditMode) headless and prints any failures.
# Unity can't open the project twice, so close the editor (or wait for a build) first.
#   usage: tools/test.sh [outdir]        (default outdir: unity/Logs)
#   UNITY=/path/to/Unity overrides the editor; the default is the Hub install of ProjectVersion.txt.
set -uo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
version=$(sed -n 's/^m_EditorVersion: //p' "$root/unity/ProjectSettings/ProjectVersion.txt")
unity=${UNITY:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity}
out=${1:-$root/unity/Logs}
[[ -x "$unity" ]] || { echo "test: no Unity $version at $unity (set UNITY=...)" >&2; exit 2; }
mkdir -p "$out" || exit 2
# Unity resolves relative test-result paths against the project, not the caller's directory.
out=$(cd "$out" && pwd -P) || exit 2
results="$out/editmode-results.xml" log="$out/editmode.log"
rm -f "$results"

start=$SECONDS
# -runTests quits by itself; -quit would end the run before the tests start.
"$unity" -batchmode -projectPath "$root/unity" -runTests -testPlatform EditMode -testResults "$results" -logFile "$log" >/dev/null 2>&1
code=$?
if [[ ! -f "$results" ]]; then
    echo "test: exit $code after $((SECONDS - start)) s with no results (log: $log)" >&2
    grep -E "error CS|warning CS|Exception:" "$log" | sort -u | head -20 >&2
    exit 1
fi
summary=$(grep -m1 -o '<test-run [^>]*>' "$results")
field() {
    local name=$1
    echo "$summary" | sed -n "s/.* $name=\"\([^\"]*\)\".*/\1/p"
    return 0
}
echo "test: $(field passed) of $(field total) passed, $(field failed) failed after $((SECONDS - start)) s (results: $results)"
# Name each failed test case.
grep -o '<test-case [^>]*result="Failed"[^>]*>' "$results" | sed -n 's/.* fullname="\([^"]*\)".*/  FAILED \1/p' | sed 's/&quot;/"/g'
problems=$(grep -E "error CS|warning CS" "$log" | sort -u)
[[ -n "$problems" ]] && echo "$problems" | head -20
[[ $code -eq 0 ]] && [[ "$(field failed)" = 0 ]] && [[ -z "$problems" ]]
