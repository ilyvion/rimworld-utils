#!/usr/bin/env bash

# This script builds a RimWorld mod and copies the necessary files to the target directory.
# Add it to your .vscode/ directory. Meant to be used with the tasks.json file from this repository.
#
# Configuration is loaded from .vscode/build_config.sh.
# The script expects the following variables to be set:
# - MOD_NAME: Name of the mod (required)
# - CONFIGURATION: Build configuration (optional, default: Debug)
# - RIMWORLD_VERSION: Version of RimWorld (optional, default: 1.6, can also be overridden by command line argument)
# - EXTRA_FILES: Array of additional files to copy (optional, default: empty)
# - SKIP_BUILD: If set to true, the build step is skipped (optional, default false)

set -euo pipefail

maybe_copy() {
    local src=$1
    local dst=$2

    if [[ ! -e $src ]]; then
        echo "No '$src'; skipping." >&2
        return 0  # not an error, just skip
    fi

    cp -r "$src" "$dst"
}

get_script_path() {
    local SOURCE="${BASH_SOURCE[0]}"
    while [ -h "$SOURCE" ]; do # resolve symlink
        local DIR
        DIR="$(cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd)"
        SOURCE="$(readlink "$SOURCE")"
        [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # relative symlink
    done
    local DIR
    DIR="$(cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd)"
    echo "$DIR/$(basename "$SOURCE")"
}

# Load configuration
CONFIG_FILE="./.vscode/build_config.sh"

# Define allowed variables
ALLOWED_VARS=(CONFIGURATION MOD_NAME TARGET RIMWORLD_VERSION EXTRA_FILES SKIP_BUILD)

# Source config in a subshell so it doesn't pollute our environment
if [[ -f "$CONFIG_FILE" ]]; then
    VARS=$(
        (
            set +u
            source "$CONFIG_FILE" || exit 1
            for var in "${ALLOWED_VARS[@]}"; do
                if [[ -v "$var" ]]; then
                    # Dump the exact declaration (preserves arrays, scalars, quotes)
                    declare -p "$var"
                fi
            done
        )
    )
    eval "$VARS"
else
    echo "Config file not found: $CONFIG_FILE" >&2
    exit 1
fi

# Ensure required variables are set or given default values
SKIP_BUILD="${SKIP_BUILD:-false}"
CONFIGURATION="${CONFIGURATION:-Debug}"
RIMWORLD_VERSION="${RIMWORLD_VERSION:-${1:-1.6}}"
: "${MOD_NAME:?MOD_NAME is not set}"

case "${SKIP_BUILD,,}" in
    true|1|yes|on)
        echo "Skipping build because SKIP_BUILD is set to true."
        exit 0
        ;;
esac

TARGET="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/Mods/$MOD_NAME"

mkdir -p .savedatafolder/$RIMWORLD_VERSION

# Build the project
dotnet build --configuration "$CONFIGURATION" --property "RimWorldVersion=$RIMWORLD_VERSION" "$MOD_NAME.sln"

# remove target mod folder
rm -rf "$TARGET"

# copy mod files
mkdir -p "$TARGET"
cp -r "$RIMWORLD_VERSION" "$TARGET/$RIMWORLD_VERSION"
maybe_copy Common "$TARGET/Common"
rsync -av --exclude='*.pdn' --exclude='*.xcf' --exclude='*.svg' --exclude='*.ttf' About/ "$TARGET/About"
maybe_copy CHANGELOG.md "$TARGET"
maybe_copy LICENSE "$TARGET"
for file in "${EXTRA_FILES[@]}"; do
    if [[ -e "$file" ]]; then
        dest="$TARGET/$file"
        mkdir -p "$(dirname "$dest")"
        maybe_copy "$file" "$dest"
    else
        echo "Warning: Extra file '$file' does not exist, skipping."
    fi
done
maybe_copy README.md "$TARGET"
maybe_copy LoadFolders.xml "$TARGET"
