#!/usr/bin/env bash

# Reconciles .deps/originals/ symlinks and README.md against originals_config.sh.
#
# Configuration is loaded from .deps/originals_config.sh.
# The script expects the following variables to be set:
# - ORIGINALS: Array of Steam Workshop item IDs to symlink (required)
# - ORIGINALS_ROOT: Directory containing the Workshop items, keyed by ID (required)

set -euo pipefail
shopt -s nullglob

get_script_dir() {
    local SOURCE="${BASH_SOURCE[0]}"
    while [ -h "$SOURCE" ]; do # resolve symlink
        local DIR
        DIR="$(cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd)"
        SOURCE="$(readlink "$SOURCE")"
        [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # relative symlink
    done
    cd -P "$(dirname "$SOURCE")" >/dev/null 2>&1 && pwd
}

confirm() {
    local reply
    read -r -p "$1 [y/N] " reply
    [[ "$reply" =~ ^[Yy] ]]
}

cd "$(get_script_dir)"

# Load configuration
CONFIG_FILE="./originals_config.sh"

# Define allowed variables
ALLOWED_VARS=(ORIGINALS ORIGINALS_ROOT)

# Source config in a subshell so it doesn't pollute our environment
if [[ -f "$CONFIG_FILE" ]]; then
    VARS=$(
        (
            set +u
            source "$CONFIG_FILE" || exit 1
            for var in "${ALLOWED_VARS[@]}"; do
                if [[ -v "$var" ]]; then
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

: "${ORIGINALS_ROOT:?ORIGINALS_ROOT is not set}"
if [[ ${#ORIGINALS[@]} -eq 0 ]]; then
    echo "ORIGINALS is not set or empty" >&2
    exit 1
fi
ORIGINALS_ROOT="${ORIGINALS_ROOT%/}"

declare -A WANTED
for id in "${ORIGINALS[@]}"; do
    WANTED[$id]=1
done

SYMLINKS_CHANGED=false
README_CHANGED=false
README="README.md"

mkdir -p originals

# Add missing / correct incorrect symlinks
for id in "${ORIGINALS[@]}"; do
    target="$ORIGINALS_ROOT/$id"
    link="originals/$id"

    if [[ -L "$link" ]]; then
        current="$(readlink "$link")"
        if [[ "$current" != "$target" ]]; then
            echo "Correcting symlink for $id (was '$current')"
            ln -sfn "$target" "$link"
            SYMLINKS_CHANGED=true
        fi
    elif [[ -e "$link" ]]; then
        echo "Warning: originals/$id exists but is not a symlink; skipping." >&2
    else
        echo "Adding symlink for $id"
        ln -s "$target" "$link"
        SYMLINKS_CHANGED=true
    fi
done

# Offer to remove symlinks not listed in ORIGINALS
for link in originals/*; do
    [[ -L "$link" ]] || continue
    id="$(basename "$link")"
    if [[ -z "${WANTED[$id]:-}" ]]; then
        if confirm "Symlink 'originals/$id' is not listed in ORIGINALS. Delete it?"; then
            rm "$link"
            echo "Deleted originals/$id"
            SYMLINKS_CHANGED=true
        fi
    fi
done

# Offer to add mods missing from README.md
for id in "${ORIGINALS[@]}"; do
    if [[ -f "$README" ]] && grep -qE "^-[[:space:]]+$id:" "$README"; then
        continue
    fi

    name=""
    about="$ORIGINALS_ROOT/$id/About/About.xml"
    if [[ -f "$about" ]]; then
        name="$(grep -m1 -oP '(?<=<name>).*?(?=</name>)' "$about" || true)"
    fi
    if [[ -z "$name" ]]; then
        read -r -p "Could not determine name for $id from its About.xml; enter a name for README.md (blank to skip): " name
    fi

    if [[ -n "$name" ]] && confirm "Add '$id: $name' to README.md?"; then
        echo "-   $id: $name" >>"$README"
        echo "Added $id to README.md"
        README_CHANGED=true
    fi
done

# Offer to remove README.md entries not listed in ORIGINALS
if [[ -f "$README" ]]; then
    while IFS= read -r line <&3; do
        if [[ "$line" =~ ^-[[:space:]]+([0-9]+): ]]; then
            id="${BASH_REMATCH[1]}"
            if [[ -z "${WANTED[$id]:-}" ]]; then
                if confirm "README.md lists '$id', which is not in ORIGINALS. Remove that line?"; then
                    grep -vF -- "$line" "$README" >"$README.tmp" && mv "$README.tmp" "$README"
                    echo "Removed $id from README.md"
                    README_CHANGED=true
                fi
            fi
        fi
    done 3<"$README"
fi

if [[ "$SYMLINKS_CHANGED" == true ]]; then
    if confirm "Symlinks changed. Run ./generate_refs.sh now?"; then
        ./generate_refs.sh
    fi
fi

if [[ "$SYMLINKS_CHANGED" == false && "$README_CHANGED" == false ]]; then
    echo "Everything is already in sync."
fi
