#!/usr/bin/env bash
# The check to run before committing: build, then run the UI flow, the gamepad flow (held and
# toggled dash), a short autoplay and the menu, settings and victory captures in parallel.
# Fails if the build or any run fails, or if any run's log shows an error or exception.
#   usage: tools/verify.sh [outdir] [--no-build]     (default outdir: $TMPDIR/deepfeast-verify)
set -uo pipefail
here=$(cd "$(dirname "$0")" && pwd)
out=${TMPDIR:-/tmp}/deepfeast-verify
build=1
for a in "$@"; do
    case $a in
        --no-build) build=0 ;;
        *) out=$a ;;
    esac
done

# Only ever clear a folder this script made.
if [ -d "$out" ] && [ -n "$(ls -A "$out")" ] && [ ! -e "$out/.deepfeast-verify" ]; then
    echo "verify: $out is not empty and was not made by verify.sh; pick another folder" >&2
    exit 2
fi
rm -rf "$out"
mkdir -p "$out"
touch "$out/.deepfeast-verify"

fail=0
if [ $build -eq 1 ]; then "$here/build.sh" "$out/build.log" || exit 1; fi

run="$here/run.sh"
pids=()
"$run" "$out" flow -interface flow -quitafter 90 & pids+=($!)
"$run" "$out" pad -padtest -quitafter 90 & pids+=($!)
"$run" "$out" pad-toggle -padtest -set dashToggle=true -quitafter 90 & pids+=($!)
"$run" "$out" auto -autoplay -timescale 3 -quitafter 40 & pids+=($!)
"$run" "$out" menu -interface menu -quitafter 12 & pids+=($!)
"$run" "$out" settings -interface settings -settingspage 3 -quitafter 12 & pids+=($!)
"$run" "$out" victory -interface victory -quitafter 12 & pids+=($!)
for pid in "${pids[@]}"; do wait "$pid" || fail=1; done

for log in "$out"/*.log; do
    [ "$(basename "$log")" = build.log ] && continue
    if grep -qE "error CS|NullReferenceException|Exception:" "$log"; then
        echo "verify: errors in $log" >&2
        fail=1
    fi
done
if [ $fail -eq 0 ]; then echo "verify: passed ($out)"; else echo "verify: FAILED ($out)" >&2; fi
exit $fail
