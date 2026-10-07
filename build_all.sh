#!/usr/bin/env bash

# Builds the mod for every RimWorld version in About/About.xml's <supportedVersions> by calling
# build.sh for each, emptying the target mod folder only before the first build so the result
# contains every version's output.
# Add it to your .vscode/ directory next to build.sh (as a symlink to this file).
# Pass versions as arguments to build only those instead.

set -euo pipefail

SCRIPT_DIR="$(cd -P "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
cd "$SCRIPT_DIR/.."

if [[ $# -gt 0 ]]; then
    versions=("$@")
else
    mapfile -t versions < <(
        sed -n '/<supportedVersions>/,/<\/supportedVersions>/s:.*<li>\s*\([0-9.]*\)\s*</li>.*:\1:p' About/About.xml |
            sort -V
    )
fi

if [[ ${#versions[@]} -eq 0 ]]; then
    echo "No versions found in About/About.xml <supportedVersions>." >&2
    exit 1
fi

first=true
for version in "${versions[@]}"; do
    echo "=== Building for RimWorld $version ==="
    if [[ $first == true ]]; then
        ./.vscode/build.sh "$version"
        first=false
    else
        SKIP_MOD_FOLDER_REMOVAL=true ./.vscode/build.sh "$version"
    fi
done
