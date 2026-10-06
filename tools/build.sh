#!/usr/bin/env bash
# Builds the macOS player headless (unity/Builds/Mac), then fails if the log shows a compiler or
# shader error, a compiler warning, an exception, or a missing art or fish-turn validation pass.
# Warnings only appear when scripts recompile, so this catches each new one as it is introduced.
#   usage: tools/build.sh [log]        (default log: unity/Logs/build.log)
#   UNITY=/path/to/Unity overrides the editor; the default is the Hub install of ProjectVersion.txt.
set -uo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
version=$(sed -n 's/^m_EditorVersion: //p' "$root/unity/ProjectSettings/ProjectVersion.txt")
unity=${UNITY:-/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity}
log=${1:-$root/unity/Logs/build.log}
[ -x "$unity" ] || { echo "build: no Unity $version at $unity (set UNITY=...)" >&2; exit 2; }
mkdir -p "$(dirname "$log")"

start=$SECONDS
"$unity" -batchmode -quit -projectPath "$root/unity" -executeMethod DeepFeast.EditorTools.Build.Mac -logFile "$log" >/dev/null 2>&1
code=$?
problems=$(grep -E "error CS|warning CS|Shader error|Exception:" "$log" | sort -u)
passes=$(grep -cE "production art validation passed|volume turns passed" "$log")
echo "build: exit $code after $((SECONDS - start)) s, $passes of 2 validations passed (log: $log)"
[ -n "$problems" ] && echo "$problems" | head -20
[ $code -eq 0 ] && [ -z "$problems" ] && [ "$passes" -eq 2 ]
