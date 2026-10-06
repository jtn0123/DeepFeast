#!/usr/bin/env bash
# Runs the built macOS player as a test run: headless, muted and always with a deadline.
#   usage: tools/run.sh <outdir> <name> [game flags...]
# Writes <outdir>/<name>.log and any screenshots to <outdir>/<name>/, prints the run's exit line
# and returns the player's exit code. A run without -quitafter gets -quitafter 300, so a forgotten
# flag can't leave a headless player running.
#   PLAYER=/path/to/binary overrides the player (default: unity/Builds/Mac/DeepFeast.app).
set -uo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
player=${PLAYER:-"$root/unity/Builds/Mac/DeepFeast.app/Contents/MacOS/Deep Feast"}
[ $# -ge 2 ] || { echo "usage: $0 <outdir> <name> [game flags...]" >&2; exit 2; }
[ -x "$player" ] || { echo "run: no player at $player (run tools/build.sh first)" >&2; exit 2; }
out=$1 name=$2
shift 2
args=("$@")
case " $* " in *" -quitafter "*) ;; *) args+=(-quitafter 300) ;; esac
mkdir -p "$out/$name"

start=$SECONDS
"$player" -batchmode -mute -shots "$out/$name" -logFile "$out/$name.log" "${args[@]}" >/dev/null 2>&1
code=$?
why=$(grep -h '\[DeepFeast\] exit' "$out/$name.log" 2>/dev/null | tail -1 | cut -c1-160)
echo "$name: exit $code after $((SECONDS - start)) s — ${why:-no exit line}"
exit $code
