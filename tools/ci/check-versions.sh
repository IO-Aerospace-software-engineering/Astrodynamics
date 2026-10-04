#!/usr/bin/env bash
# Fails when the documented versions disagree with the <Version> of the library or CLI project:
# the "Current Versions" table of docs/contributing/versioning.md and the "Version Information"
# section of DEVELOPER_GUIDE.md. Run from anywhere: paths are resolved from the repo root.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
doc="$root/docs/contributing/versioning.md"
guide="$root/DEVELOPER_GUIDE.md"
lib_csproj="$root/IO.Astrodynamics.Net/IO.Astrodynamics/IO.Astrodynamics.csproj"
cli_csproj="$root/IO.Astrodynamics.Net/IO.Astrodynamics.CLI/IO.Astrodynamics.CLI.csproj"

# Version in the table row whose first cell contains the given package id, e.g.
# | NuGet (`IO.Astrodynamics`) | `10.0.0` |
doc_version() {
    grep -E "^\| [^|]*\(\`$1\`\) *\|" "$doc" | head -n 1 | sed -E 's/.*\| *`([^`]+)` *\| *$/\1/'
}

# Version on a "- <label>: <version>" line of the developer guide, e.g. "- NuGet Package: 10.0.0".
guide_version() {
    grep -E "^- $1: " "$guide" | head -n 1 | sed -E "s/^- $1: *([^ ]+).*/\1/"
}

csproj_version() {
    grep -oE '<Version>[^<]+</Version>' "$1" | head -n 1 | sed -E 's|<Version>(.*)</Version>|\1|'
}

status=0
check() {
    local label="$1" documented="$2" declared="$3"
    if [[ -z "$documented" || -z "$declared" ]]; then
        echo "::error::$label: version not found (documented='$documented', csproj='$declared')"
        status=1
    elif [[ "$documented" != "$declared" ]]; then
        echo "::error::$label: documented $documented but the csproj declares $declared"
        status=1
    else
        echo "$label: $declared"
    fi
}

lib_version="$(csproj_version "$lib_csproj")"
cli_version="$(csproj_version "$cli_csproj")"

check "versioning.md, IO.Astrodynamics" "$(doc_version 'IO.Astrodynamics')" "$lib_version"
check "versioning.md, IO.Astrodynamics.CLI" "$(doc_version 'IO.Astrodynamics.CLI')" "$cli_version"
check "DEVELOPER_GUIDE.md, NuGet Package" "$(guide_version 'NuGet Package')" "$lib_version"
check "DEVELOPER_GUIDE.md, CLI Tool" "$(guide_version 'CLI Tool')" "$cli_version"

exit $status
