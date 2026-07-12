#!/usr/bin/env bash

# Launches a RimWorld mod project's game under GABS (CustomCommand launch
# mode) so an MCP client can drive RimBridgeServer, without touching the
# VS Code debug launch configs in launch.json.
#
# Add it to your .vscode/ directory (symlinked, like build.sh/tasks.json/
# launch.json from this repository), then register it with GABS:
#
#   gabs games add <game-id>
#     Launch Mode: CustomCommand
#     Target: <absolute path to project>/.vscode/gabs-launch.sh
#     Working Directory: <absolute path to project>   <-- required, see below
#
# GABS execs its CustomCommand target directly (no shell), so a bare command
# string can't do `$GABP_SERVER_PORT`-style expansion of the env vars GABS
# injects. This script exists to read those real process env vars and
# forward them into the Flatpak Steam sandbox, which does not inherit host
# env vars on its own.
#
# Working Directory must be set explicitly in `gabs games add`: GABS leaves
# the child process's cwd as whatever `gabs server` itself was started from
# when this field is left blank, and this script locates the project by its
# own working directory (`$(pwd)`), not by its own location on disk.
#
# Configuration is loaded from .vscode/build_config.sh, the same file
# build.sh reads. Recognized variables (all optional):
# - RIMWORLD_VERSION: Version of RimWorld to launch (default: 1.6)
# - RIMWORLD_EXECUTABLE: Path to the RimWorld executable inside the Flatpak
#   Steam install (default: standard Flatpak Steam location)
# - GABS_LAUNCH_ARGS: Array of extra RimWorld command-line arguments

set -euo pipefail

PROJECT_DIR="$(pwd)"
CONFIG_FILE="$PROJECT_DIR/.vscode/build_config.sh"

ALLOWED_VARS=(RIMWORLD_VERSION RIMWORLD_EXECUTABLE GABS_LAUNCH_ARGS)

RIMWORLD_VERSION=""
RIMWORLD_EXECUTABLE=""
GABS_LAUNCH_ARGS=()

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
fi

RIMWORLD_VERSION="${RIMWORLD_VERSION:-1.6}"
RIMWORLD_EXECUTABLE="${RIMWORLD_EXECUTABLE:-$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux}"

SAVE_DIR="$PROJECT_DIR/.savedatafolder/$RIMWORLD_VERSION"
mkdir -p "$SAVE_DIR"

exec flatpak-spawn --host flatpak run \
  --filesystem="$PROJECT_DIR" \
  --env=HARMONY_LOG_FILE="$SAVE_DIR/Harmony.log" \
  --env=GABP_SERVER_PORT="${GABP_SERVER_PORT:?GABP_SERVER_PORT not set; run this script through GABS}" \
  --env=GABP_TOKEN="${GABP_TOKEN:?GABP_TOKEN not set; run this script through GABS}" \
  --env=GABS_GAME_ID="${GABS_GAME_ID:-rimworld}" \
  --command="$RIMWORLD_EXECUTABLE" \
  com.valvesoftware.Steam \
  -logfile "$SAVE_DIR/Player.log" \
  -savedatafolder="$SAVE_DIR" \
  "${GABS_LAUNCH_ARGS[@]}"
