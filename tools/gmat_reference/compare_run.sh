#!/usr/bin/env bash
# Check that a run of run_gmat.sh wrote the committed reference data (H3 review of #408, N7).
#
# Usage: tools/gmat_reference/compare_run.sh <committed directory> <directory of the run>
#
# From the repository root. Compares both ways, so that a file missing from the run or written by the run but never
# committed fails as well as a changed one: the reports and the partials byte for byte; the console logs but for the
# date of the run and its duration, the only lines of a log that change from one run to the next; the scripts
# generated from cases.json against the committed ones, untracked files included.
set -uo pipefail

committed=$1
run=$2
status=0

# cases.json and the converted field are inputs of the run, not outputs
diff -r --exclude='*.log' --exclude='cases.json' --exclude='*.cof' "$committed" "$run" || status=1

logs() { (cd "$1" && find . -name "*.log" | sort); }
diff <(logs "$committed") <(logs "$run") || status=1
stable() {
  sed -E -e 's/^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} (GMAT Moderator)/\1/' \
    -e 's/^(===> Total Run Time:) [0-9.]+ seconds$/\1/' "$1"
}
for log in $(logs "$committed"); do
  [ -f "$run/$log" ] && { diff -q <(stable "$committed/$log") <(stable "$run/$log") > /dev/null || { echo "differs: $log"; status=1; }; }
done

if [ -n "$(git status --porcelain -- tools/gmat_reference/scripts)" ]; then
  git status --porcelain -- tools/gmat_reference/scripts
  status=1
fi
exit $status
