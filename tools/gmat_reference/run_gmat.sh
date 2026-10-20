#!/usr/bin/env bash
# Run the GMAT scripts of the comparison of IO.Astrodynamics with GMAT (H2, #408) and collect their reports.
#
# Usage: tools/gmat_reference/run_gmat.sh <GMAT R2026a directory> [output directory]
#
# From the repository root. The scripts are generated from cases.json into a scratch directory, the placeholders
# replaced (@SPK@: de440s.bsp of the test data; @COF@: the converted EGM2008; @OUT@: the output directory), and each one
# run by GmatConsole from GMAT's bin directory. The reports and the console logs land in the output directory, by
# default IO.Astrodynamics.Tests/Data/GmatReference, one directory per case. Stops at the first failing script.
set -euo pipefail

gmat=$(cd "$1" && pwd)
root=$(pwd)
data="$root/IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data"
out=$(mkdir -p "${2:-$data/GmatReference}" && cd "${2:-$data/GmatReference}" && pwd)
tool="$root/tools/gmat_reference"
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT

cases="$data/GmatReference/cases.json"
cof="$data/GmatReference/EGM2008_to70_TideFree_IO.cof"
spk="$data/SolarSystem/de440s.bsp"

# The scripts and the reports are written again from scratch, so that none is left from an earlier set of cases
rm -rf "$tool/scripts"
python3 -I -B "$tool/generate_scripts.py" "$cases" "$tool/scripts"
for directory in "$tool"/scripts/*/; do
  rm -rf "${out:?}/$(basename "$directory")"
done
python3 -I -B "$tool/check_cof.py" "$data/SolarSystem/EGM2008_to70_TideFree" "$cof" "$cases"

for script in "$tool"/scripts/*/*.script; do
  case=$(basename "$(dirname "$script")")
  variant=$(basename "$script" .script)
  mkdir -p "$out/$case" "$scratch/$case"
  sed -e "s#@SPK@#$spk#g" -e "s#@COF@#$cof#g" -e "s#@OUT@#$out#g" "$script" > "$scratch/$case/$variant.script"
  rm -f "$out/$case/$variant.txt"
  if ! (cd "$gmat/bin" && ./GmatConsole --run "$scratch/$case/$variant.script") > "$out/$case/$variant.log" 2>&1; then
    echo "GMAT failed on $case/$variant, see $out/$case/$variant.log" >&2
    exit 1
  fi
  if grep -q "\*\*\*\* ERROR" "$out/$case/$variant.log" || [ ! -s "$out/$case/$variant.txt" ]; then
    echo "GMAT reported an error on $case/$variant, see $out/$case/$variant.log" >&2
    exit 1
  fi
  # The logs are committed: the paths of the machine replaced by the placeholders of the scripts
  sed -i -e "s#$scratch#@SCRATCH@#g" -e "s#$spk#@SPK@#g" -e "s#$cof#@COF@#g" -e "s#$out#@OUT@#g" -e "s#$gmat#@GMAT@#g" \
    "$out/$case/$variant.log"
  echo "$case/$variant"
done

# The A matrix of each force at the B7 states, through GMAT's own Python API (its startup file with absolute paths
# first, as GMAT's api/API_README.txt asks), one process per state and force, earth_point_mass first
(cd "$gmat/api" && python3 -I -B BuildApiStartupFile.py > /dev/null)
rm -rf "${out:?}/partials"
python3 -I -B -c 'import json, sys
for s in json.load(open(sys.argv[1]))["partials"]["states"]:
    for f in s["forces"]:
        print(s["id"], f)' "$cases" | while read -r state force; do
  python3 -I -B "$tool/gmat_partials.py" "$gmat/bin" "$cases" "$spk" "$cof" "$out" "$state" "$force" > /dev/null
  echo "partials/${state}_${force}"
done
