#!/usr/bin/env bash

# Launches a RimWorld mod project's game (1.7 and later, which run on CoreCLR) so a debugger can
# attach before any game or mod code runs. Run it from the project folder; tasks.json's
# "start 1.7 for debugging" task does, as the preLaunchTask of launch.json's attach config.
#
# The game runs inside Steam's Flatpak sandbox and Steam Linux Runtime container, which have their
# own /tmp and PID namespace. The runtime names its debugger pipes and diagnostics socket after its
# PID inside that namespace, in that /tmp, so a debugger on the host can't find them. Once they
# exist, this script symlinks them into the host's temp folder under the game's host PID, and it
# removes the links when the game exits.
#
# The game is started with the WaitForDebugger startup hook (WaitForDebugger/ next to this script,
# built into .savedatafolder/<version>/WaitForDebugger), which holds it before RimWorld's Main runs
# until a debugger attaches, or until WAIT_FOR_DEBUGGER_TIMEOUT seconds (default 60) have passed.
# Once the links exist, the script prints "RimWorld is waiting for a debugger (PID <pid>)", the
# line tasks.json's problem matcher waits for. Stopping the script (e.g. terminating the task)
# also closes the game.
#
# Configuration is loaded from .vscode/build_config.sh, the same file build.sh reads. Recognized
# variables (all optional):
# - STEAM_COMMON: The Flatpak Steam install's steamapps/common folder
#   (default: standard Flatpak Steam location)
# - RIMWORLD_EXECUTABLE: Path to the RimWorld executable (default: RimWorld/RimWorld under
#   STEAM_COMMON)
#
# The first argument is the RimWorld version (default: 1.7); any further arguments are passed to
# RimWorld.

set -euo pipefail

PROJECT_DIR="$(pwd)"
SCRIPT_DIR="$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"
CONFIG_FILE="$PROJECT_DIR/.vscode/build_config.sh"

ALLOWED_VARS=(STEAM_COMMON RIMWORLD_EXECUTABLE)

STEAM_COMMON=""
RIMWORLD_EXECUTABLE=""

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

RIMWORLD_VERSION="${1:-1.7}"
[[ $# -gt 0 ]] && shift
if [[ "$RIMWORLD_VERSION" == 1.[0-6] ]]; then
    echo "RimWorld $RIMWORLD_VERSION runs on Mono, not CoreCLR; only 1.7 and later are supported" >&2
    exit 1
fi

STEAM_COMMON="${STEAM_COMMON:-$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common}"
RIMWORLD_EXECUTABLE="${RIMWORLD_EXECUTABLE:-$STEAM_COMMON/RimWorld/RimWorld}"
GAME_DIR="$(dirname "$RIMWORLD_EXECUTABLE")"
SAVE_DIR="$PROJECT_DIR/.savedatafolder/$RIMWORLD_VERSION"
HOOK_DIR="$SAVE_DIR/WaitForDebugger"
HOST_TMP="${TMPDIR:-/tmp}"
# Where the Steam Flatpak's /tmp is visible on the host.
FLATPAK_TMP="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}/.flatpak/com.valvesoftware.Steam/tmp"

# Prints the host PID of the game process launched with this project's save folder. Its
# -savedatafolder argument is unique per project; the Flatpak and container processes wrapping it
# carry the same argument, so the process name tells the game itself apart.
find_game_pid() {
    local pid
    for pid in $(pgrep -f -- "-savedatafolder=$SAVE_DIR"); do
        if [[ "$(cat "/proc/$pid/comm" 2>/dev/null)" == RimWorld ]]; then
            echo "$pid"
            return 0
        fi
    done
    return 1
}

if find_game_pid > /dev/null; then
    echo "RimWorld is already running with $SAVE_DIR; close it first" >&2
    exit 1
fi

steam_pid=$(pgrep -o -f '/ubuntu12_32/steam ') || {
    echo 'Steam is not running' >&2
    exit 1
}

mkdir -p "$SAVE_DIR"
dotnet build "$SCRIPT_DIR/WaitForDebugger/WaitForDebugger.csproj" \
    --configuration Release --nologo --verbosity quiet --output "$HOOK_DIR"

LINKS=()
remove_links() {
    rm -f -- "${LINKS[@]}"
}
stop_game() {
    pkill -f -- "-savedatafolder=$SAVE_DIR" || true
    exit 130
}
trap remove_links EXIT
trap stop_game INT TERM HUP

echo "Starting RimWorld $RIMWORLD_VERSION"
# Joins Steam's sandbox (--parent-pid) the way Steam's own launches do, and writes the game's
# output, minus terminal color codes, to Player.log, since 1.7 has no -logfile option.
flatpak run --parent-pid="$steam_pid" --parent-share-pids \
    --filesystem="$PROJECT_DIR" \
    --filesystem="$GAME_DIR" \
    --env=HARMONY_LOG_FILE="$SAVE_DIR/Harmony.log" \
    --env=PRESSURE_VESSEL_FILESYSTEMS_RW="$PROJECT_DIR:$GAME_DIR" \
    --env=SteamAppId=294100 \
    --env=SteamGameId=294100 \
    --env=DOTNET_STARTUP_HOOKS="$HOOK_DIR/WaitForDebugger.dll" \
    --env=WAIT_FOR_DEBUGGER_TIMEOUT="${WAIT_FOR_DEBUGGER_TIMEOUT:-60}" \
    --command="$STEAM_COMMON/SteamLinuxRuntime_soldier/_v2-entry-point" \
    com.valvesoftware.Steam \
    --verb=waitforexitandrun \
    -- \
    "$STEAM_COMMON/SteamLinuxRuntime/scout-on-soldier-entry-point-v2" \
    -- \
    "$RIMWORLD_EXECUTABLE" \
    -NoFileLog \
    -savedatafolder="$SAVE_DIR" \
    "$@" 2>&1 \
    | tee >(sed -u 's/\x1b\[[0-9;]*[A-Za-z]//g' > "$SAVE_DIR/Player.log") &
LAUNCH_PID=$!

# Fails the script if the game (or its launch) has exited.
ensure_running() {
    if ! kill -0 "$1" 2> /dev/null; then
        echo "RimWorld exited before a debugger could attach" >&2
        exit 1
    fi
}

game_pid=""
until game_pid="$(find_game_pid)"; do
    ensure_running "$LAUNCH_PID"
    sleep 0.1
done

ns_pid="$(awk '/^NSpid:/ { print $NF }' "/proc/$game_pid/status")"
start_time="$(awk '{ print $22 }' "/proc/$game_pid/stat")"
game_tmp="$(tr '\0' '\n' < "/proc/$game_pid/environ" | sed -n 's/^TMPDIR=//p')"
game_tmp="/proc/$game_pid/root${game_tmp:-/tmp}"

names=(
    "clr-debug-pipe-$ns_pid-$start_time-in"
    "clr-debug-pipe-$ns_pid-$start_time-out"
    "dotnet-diagnostic-$ns_pid-$start_time-socket"
)
for name in "${names[@]}"; do
    until [[ -e "$game_tmp/$name" ]]; do
        ensure_running "$game_pid"
        sleep 0.1
    done
done

for name in "${names[@]}"; do
    # The Flatpak's /tmp path is the one known to work for attaching; the path through /proc is
    # only used when the game's temp folder isn't that one.
    source_path="$game_tmp/$name"
    if [[ "$FLATPAK_TMP/$name" -ef "$source_path" ]]; then
        source_path="$FLATPAK_TMP/$name"
    fi
    link="$HOST_TMP/${name/-$ns_pid-/-$game_pid-}"
    if [[ "$link" -ef "$source_path" ]]; then
        continue
    fi
    ln -sfn -- "$source_path" "$link"
    LINKS+=("$link")
done

echo "RimWorld is waiting for a debugger (PID $game_pid)"

set +e
wait "$LAUNCH_PID"
GAME_EXIT=$?
set -e
exit "$GAME_EXIT"
