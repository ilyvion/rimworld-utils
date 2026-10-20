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
# - RIMWORLD_VERSION: Version of RimWorld to launch (default: 1.7)
# - STEAM_COMMON: The Flatpak Steam install's steamapps/common folder
#   (default: standard Flatpak Steam location)
# - GAMES_DIR: Folder holding copies of older RimWorld versions, one per version, as
#   <GAMES_DIR>/<version>/RimWorld (default: Games/ next to this script's resolved location,
#   i.e. in rimworld-utils)
# - RIMWORLD_EXECUTABLE: Path to the RimWorld executable (default:
#   GAMES_DIR/<version>/RimWorld/RimWorldLinux for 1.6 and earlier, RimWorld/RimWorld under
#   STEAM_COMMON for 1.7 and later)
# - GABS_LAUNCH_ARGS: Array of extra RimWorld command-line arguments
# - MOD_NAME / TARGET: Where build.sh installs the mod (only needed for DevTools test runs)
#
# Every version runs inside the Steam Linux Runtime, as Steam itself launches them. RimWorld 1.7
# and later have no -logfile option, so their output is written to Player.log instead.
#
# Arguments GABS passes to this script (its `args`, profile args and launch inputs) are forwarded
# to RimWorld, except `--rimworld-version <version>`, which overrides RIMWORLD_VERSION for this
# launch. To pick the version per launch and for DevTools test runs (see devtools.md), declare
# launch inputs in the GABS game config, e.g.:
#
#   "launchInputs": {
#     "rimworldVersion": {
#       "description": "RimWorld version to build and launch",
#       "type": "string",
#       "pattern": "1\\.[0-9]+",
#       "args": ["--rimworld-version", "${value}"]
#     },
#     "runTests": {
#       "description": "Run the DevTools test suite, then quit the game",
#       "type": "boolean",
#       "args": ["--test", "--exit"]
#     },
#     "testFilter": {
#       "description": "DevTools filter expression, e.g. class == WithCountTests",
#       "type": "string",
#       "pattern": "[A-Za-z0-9_.=!~&| -]+",
#       "args": ["--where", "${value}"]
#     }
#   }
#
# and start the game via the MCP `games_start` tool with `launchInputs: {"runTests": true}`. When `--test` is
# among the forwarded arguments, this script adds `--pid <packageId from About/About.xml>` and
# writes Configs/$CONFIG_NAME.xml into the installed mod folder (the project's own
# Configs/$CONFIG_NAME.xml, if any, plus a <log> element) so DevTools writes its Test.log to
# .savedatafolder/<version>/Test.log (next to Player.log). It also keeps timestamped backups of
# .savedatafolder/<version>/Config/ModsConfig.xml (ModsConfig.xml.bak-*, newest 5) before test
# runs, and restores the latest one once the game exits, however it exits.
#
# Runs .vscode/build.sh $RIMWORLD_VERSION before launching, so the mod
# folder the game loads from is always up to date for the version being
# launched.

set -euo pipefail

PROJECT_DIR="$(pwd)"
CONFIG_FILE="$PROJECT_DIR/.vscode/build_config.sh"

ALLOWED_VARS=(MOD_NAME TARGET RIMWORLD_VERSION STEAM_COMMON GAMES_DIR RIMWORLD_EXECUTABLE GABS_LAUNCH_ARGS)

MOD_NAME=""
TARGET=""
RIMWORLD_VERSION=""
STEAM_COMMON=""
GAMES_DIR=""
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

FORWARDED_ARGS=()
while [[ $# -gt 0 ]]; do
    if [[ "$1" == "--rimworld-version" && $# -ge 2 ]]; then
        RIMWORLD_VERSION="$2"
        shift 2
    else
        FORWARDED_ARGS+=("$1")
        shift
    fi
done

RIMWORLD_VERSION="${RIMWORLD_VERSION:-1.7}"
STEAM_COMMON="${STEAM_COMMON:-$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common}"
GAMES_DIR="${GAMES_DIR:-$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")/Games}"
if [[ "$RIMWORLD_VERSION" == 1.[0-6] ]]; then
    HAS_LOGFILE=true
    RIMWORLD_EXECUTABLE="${RIMWORLD_EXECUTABLE:-$GAMES_DIR/$RIMWORLD_VERSION/RimWorld/RimWorldLinux}"
else
    HAS_LOGFILE=false
    RIMWORLD_EXECUTABLE="${RIMWORLD_EXECUTABLE:-$STEAM_COMMON/RimWorld/RimWorld}"
fi
GAME_DIR="$(dirname "$RIMWORLD_EXECUTABLE")"

SAVE_DIR="$PROJECT_DIR/.savedatafolder/$RIMWORLD_VERSION"
mkdir -p "$SAVE_DIR"

"$PROJECT_DIR/.vscode/build.sh" "$RIMWORLD_VERSION"

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
    # -NoWindow (headless-style launch) exists in 1.7+; 1.6 and earlier are not known to support it.
    [[ "$HAS_LOGFILE" == false ]] && FORWARDED_ARGS+=(-NoWindow)
    TARGET="${TARGET:-$STEAM_COMMON/RimWorld/Mods/$MOD_NAME}"
    # build.sh empties the mod folder, so the config is written after it. Test.log is truncated
    # by DevTools at the start of each run; remove it too so a stale one can't be read.
    mkdir -p "$TARGET/Configs"
    # The project's own Configs/$CONFIG_NAME.xml (optional; DevTools settings such as
    # warningsAllowed/errorsAllowed, without a <log> element) is merged with the log folder.
    # A test plan's child process runs unit tests, so UnitTestConfig is written alongside.
    for name in "$CONFIG_NAME" UnitTestConfig; do
        if [[ -f "$PROJECT_DIR/Configs/$name.xml" ]]; then
            cp "$PROJECT_DIR/Configs/$name.xml" "$TARGET/Configs/$name.xml"
        else
            printf '<TestConfig>\n</TestConfig>\n' > "$TARGET/Configs/$name.xml"
        fi
        sed -i "s#</TestConfig>#<log><folder>$SAVE_DIR</folder></log></TestConfig>#" "$TARGET/Configs/$name.xml"
    done
    rm -f "$SAVE_DIR/Test.log"

    # DevTools test plans can rewrite ModsConfig.xml. Keep timestamped copies (only when the
    # content changed, newest 5) so a run's actual mod list stays available, and remember the
    # latest one in RESTORE_BACKUP so restore_mods_config (below) can put it back once the game
    # exits.
    MODS_CONFIG="$SAVE_DIR/Config/ModsConfig.xml"
    RESTORE_BACKUP=""
    if [[ -f "$MODS_CONFIG" ]]; then
        LATEST_BACKUP="$(ls -1t "$MODS_CONFIG".bak-* 2>/dev/null | head -1 || true)"
        if [[ -z "$LATEST_BACKUP" ]] || ! cmp -s "$MODS_CONFIG" "$LATEST_BACKUP"; then
            cp "$MODS_CONFIG" "$MODS_CONFIG.bak-$(date +%Y%m%d-%H%M%S)"
        fi
        ls -1t "$MODS_CONFIG".bak-* 2>/dev/null | tail -n +6 | xargs -r rm --
        RESTORE_BACKUP="$(ls -1t "$MODS_CONFIG".bak-* 2>/dev/null | head -1)"
    fi
fi

# Puts RESTORE_BACKUP (see above) back over ModsConfig.xml once the game has exited, so a
# DevTools test run never leaves the mod list altered no matter how the game terminated. A no-op
# outside test runs, or when no pre-run ModsConfig.xml existed to back up.
restore_mods_config() {
    [[ "$RUNNING_TESTS" == true && -n "$RESTORE_BACKUP" ]] && cp "$RESTORE_BACKUP" "$MODS_CONFIG"
}

# flatpak-spawn --host hands the game off to a host-side process tree that isn't a descendant of
# this script (or of the flatpak-spawn client below), so a signal sent to either of those never
# reaches the game. GABS tracks the flatpak-spawn client's PID, so `games_stop`/`games_kill` need
# a trap here that reaches the real game process by another means: its -savedatafolder argument is
# unique per project, so it also can't affect another project's or an unrelated RimWorld instance.
stop_game() {
    flatpak-spawn --host pkill -f -- "-savedatafolder=$SAVE_DIR" || true
    wait "$GAME_PID" 2>/dev/null
    restore_mods_config
    exit 0
}
trap stop_game TERM INT

if [[ "$HAS_LOGFILE" == true ]]; then
    CAPTURED_LOG=""
    LOG_ARGS=(-logfile "$SAVE_DIR/Player.log")
else
    CAPTURED_LOG="$SAVE_DIR/Player.log"
    # -NoFileLog (1.7+) keeps the game from writing its own Player.log into the main game directory.
    LOG_ARGS=(-NoFileLog)
fi

# Runs on the host: joins Steam's sandbox (--parent-pid) the way Steam's own launches do, and, when
# given a log path, writes the game's output, minus terminal color codes, to it.
flatpak-spawn --host bash -c '
    set -o pipefail
    log=$1
    shift
    steam_pid=$(pgrep -o -f "/ubuntu12_32/steam ") || { echo "Steam is not running" >&2; exit 1; }
    if [[ -z "$log" ]]; then
        exec flatpak run --parent-pid="$steam_pid" --parent-share-pids "$@"
    fi
    flatpak run --parent-pid="$steam_pid" --parent-share-pids "$@" 2>&1 \
        | sed -u "s/\x1b\[[0-9;]*[A-Za-z]//g" > "$log"
' bash "$CAPTURED_LOG" \
  --filesystem="$PROJECT_DIR" \
  --filesystem="$GAME_DIR" \
  --env=HARMONY_LOG_FILE="$SAVE_DIR/Harmony.log" \
  --env=GABP_SERVER_PORT="${GABP_SERVER_PORT:?GABP_SERVER_PORT not set; run this script through GABS}" \
  --env=GABP_TOKEN="${GABP_TOKEN:?GABP_TOKEN not set; run this script through GABS}" \
  --env=GABS_GAME_ID="${GABS_GAME_ID:-rimworld}" \
  --env=PRESSURE_VESSEL_FILESYSTEMS_RW="$PROJECT_DIR:$GAME_DIR" \
  --env=SteamAppId=294100 \
  --env=SteamGameId=294100 \
  --command="$STEAM_COMMON/SteamLinuxRuntime_soldier/_v2-entry-point" \
  com.valvesoftware.Steam \
  --verb=waitforexitandrun \
  -- \
  "$STEAM_COMMON/SteamLinuxRuntime/scout-on-soldier-entry-point-v2" \
  -- \
  "$RIMWORLD_EXECUTABLE" \
  "${LOG_ARGS[@]}" \
  -savedatafolder="$SAVE_DIR" \
  "${GABS_LAUNCH_ARGS[@]}" \
      "${FORWARDED_ARGS[@]}" &
GAME_PID=$!
set +e
wait "$GAME_PID"
GAME_EXIT=$?
set -e
restore_mods_config
exit "$GAME_EXIT"
