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
# - MOD_NAME / TARGET: Where build.sh installs the mod (only needed for DevTools test runs)
#
# Arguments GABS passes to this script (its `args`, profile args and launch inputs) are forwarded
# to RimWorld. For DevTools test runs (see devtools.md), declare launch inputs in the GABS game
# config, e.g.:
#
#   "launchInputs": {
#     "runTests": {
#       "description": "Run the DevTools test suite, then quit the game",
#       "type": "boolean",
#       "args": ["--test", "--exit"]
#     },
#     "testFilter": {
#       "description": "DevTools filter expression, e.g. class == WithCountTests",
#       "type": "string",
#       "pattern": "[A-Za-z0-9_.=!~&| -]+",
#       "args": ["--filter", "${value}"]
#     }
#   }
#
# and start the game via the MCP `games_start` tool with `launchInputs: {"runTests": true}`. When `--test` is
# among the forwarded arguments, this script adds `--pid <packageId from About/About.xml>` and
# writes Configs/$CONFIG_NAME.xml into the installed mod folder (the project's own
# Configs/$CONFIG_NAME.xml, if any, plus a <log> element) so DevTools writes its Test.log to
# .savedatafolder/<version>/Test.log (next to Player.log). It also keeps timestamped backups of
# .savedatafolder/<version>/Config/ModsConfig.xml (ModsConfig.xml.bak-*, newest 5) before test runs.
#
# Runs .vscode/build.sh $RIMWORLD_VERSION before launching, so the mod
# folder the game loads from is always up to date for the version being
# launched.

set -euo pipefail

PROJECT_DIR="$(pwd)"
CONFIG_FILE="$PROJECT_DIR/.vscode/build_config.sh"

ALLOWED_VARS=(MOD_NAME TARGET RIMWORLD_VERSION RIMWORLD_EXECUTABLE GABS_LAUNCH_ARGS)

MOD_NAME=""
TARGET=""
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

"$PROJECT_DIR/.vscode/build.sh" "$RIMWORLD_VERSION"

FORWARDED_ARGS=("$@")
RUNNING_TESTS=false
CONFIG_NAME=UnitTestConfig
HAS_PID=false
for arg in "${FORWARDED_ARGS[@]}"; do
    case "$arg" in
        --test|-t) RUNNING_TESTS=true; CONFIG_NAME=UnitTestConfig ;;
        --test-plan|-p) RUNNING_TESTS=true; CONFIG_NAME=TestPlanConfig ;;
        --smoke-test|-s) RUNNING_TESTS=true; CONFIG_NAME=SmokeTestConfig ;;
    esac
    [[ "$arg" == "--pid" ]] && HAS_PID=true
done

if [[ "$RUNNING_TESTS" == true ]]; then
    if [[ "$HAS_PID" == false ]]; then
        PACKAGE_ID="$(sed -n 's:.*<packageId>\(.*\)</packageId>.*:\1:p' "$PROJECT_DIR/About/About.xml" | head -1)"
        FORWARDED_ARGS=(--pid "$PACKAGE_ID" "${FORWARDED_ARGS[@]}")
    fi
    TARGET="${TARGET:-$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/Mods/$MOD_NAME}"
    # build.sh empties the mod folder, so the config is written after it. Test.log is truncated
    # by DevTools at the start of each run; remove it too so a stale one can't be read.
    mkdir -p "$TARGET/Configs"
    # The project's own Configs/$CONFIG_NAME.xml (optional; DevTools settings such as
    # warningsAllowed/errorsAllowed, without a <log> element) is merged with the log folder.
    # A test plan's child process runs unit tests, so UnitTestConfig is written alongside.
    if [[ -f "$PROJECT_DIR/Configs/$CONFIG_NAME.xml" ]]; then
        cp "$PROJECT_DIR/Configs/$CONFIG_NAME.xml" "$TARGET/Configs/$CONFIG_NAME.xml"
    else
        printf '<TestConfig>\n</TestConfig>\n' > "$TARGET/Configs/$CONFIG_NAME.xml"
    fi
    sed -i "s#</TestConfig>#<log><folder>$SAVE_DIR</folder></log></TestConfig>#" "$TARGET/Configs/$CONFIG_NAME.xml"
    rm -f "$SAVE_DIR/Test.log"

    # DevTools test plans can rewrite ModsConfig.xml and only restore it on a clean finish, and the
    # game is exec'd below so nothing here can restore it afterwards. Keep timestamped copies
    # (only when the content changed, newest 5) so a good mod list survives an interrupted run;
    # restore by copying the wanted ModsConfig.xml.bak-* over ModsConfig.xml.
    MODS_CONFIG="$SAVE_DIR/Config/ModsConfig.xml"
    if [[ -f "$MODS_CONFIG" ]]; then
        LATEST_BACKUP="$(ls -1t "$MODS_CONFIG".bak-* 2>/dev/null | head -1 || true)"
        if [[ -z "$LATEST_BACKUP" ]] || ! cmp -s "$MODS_CONFIG" "$LATEST_BACKUP"; then
            cp "$MODS_CONFIG" "$MODS_CONFIG.bak-$(date +%Y%m%d-%H%M%S)"
        fi
        ls -1t "$MODS_CONFIG".bak-* 2>/dev/null | tail -n +6 | xargs -r rm --
    fi
fi

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
  "${GABS_LAUNCH_ARGS[@]}" \
  "${FORWARDED_ARGS[@]}"
