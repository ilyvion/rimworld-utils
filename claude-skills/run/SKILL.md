---
name: run
description: "Launch and drive this RimWorld mod project in a live game session via GABS/RimBridgeServer for AI-driven verification, screenshots, mod inspection, and debug-action testing. Use when asked to run, start, or screenshot the game, or to confirm a change works in the real game."
---

# Running a RimWorld mod project via GABS

This project is driven through **GABS** (a local MCP server) bridging to **RimBridgeServer** (the in-game GABP bridge), not by launching the game directly. This is a separate path from the project's VS Code F5 debug launch configs (`.vscode/launch.json`) — it does not run those configs' `preLaunchTask` build step, so **build the mod first** if you're testing a code change.

**Build with `.vscode/build.sh <version>` (e.g. `.vscode/build.sh 1.6`) — never a bare `dotnet build`.** `dotnet build` only compiles to `Source/*/bin`; it does not copy anything into the actual RimWorld mod folder the game reads from. `build.sh` does the `dotnet build` *and* wipes/repopulates that mod folder (assemblies, `About/`, `Common/`, etc.) from the build output. Skipping it means GABS launches the game against stale or missing mod files — any verification done that way is meaningless, even if it looks like it ran fine.

The GABS `gameId` convention for these mod projects is **the project's own directory name** (e.g. a repo at `.../RimWorld/colony-manager-redux` registers as `gameId: colony-manager-redux`). Determine it from the current working directory's basename unless told otherwise.

## Prerequisites

The GABS tools (`mcp__gabs__*`) are deferred — load them first with `ToolSearch`, e.g. `select:mcp__gabs__games_list,mcp__gabs__games_status,mcp__gabs__games_start,mcp__gabs__games_connect,mcp__gabs__games_stop,mcp__gabs__games_get_attention,mcp__gabs__games_ack_attention,mcp__gabs__games_tool_names,mcp__gabs__games_tool_detail,mcp__gabs__games_call_tool`.

Confirm this project is registered: `games_list` should include this project's `gameId`. If it isn't, **stop and tell the user** — do not attempt to `gabs games add` it yourself; that requires interactive choices (executable paths, Steam library layout) the user needs to make. Point them at this repo's `CLAUDE.md` section "Launching RimWorld under GABS for AI-driven bridge sessions" for the setup steps (`gabs-launch.sh` wrapper, required `Working Directory`, required `stopProcessName: RimWorldLinux`).

## Launching

1. `games_status` with the project's `gameId` first, to see if it's already running.
   - `stopped` → call `games_start`.
   - `running-disconnected` → call `games_connect` (don't restart the game).
   - `running` with tools available → already good to go.
2. The GABP connection sometimes drops once, early in startup, right after `games_start` reports success (`games_call_tool` fails with "GABP client connection closed"). This is expected — just call `games_connect` again once; no need to restart the game or treat it as a failure.
3. Confirm the tool surface came through with `games_tool_names` (brief=true) — expect on the order of 100+ `rimbridge/*`/`rimworld/*` tools once connected.

## Waiting for the game to be ready

There is no dedicated "wait for main menu" tool. `rimbridge/wait_for_game_loaded`'s readiness targets (`gameData`, `mapData`, `currentMap`, `playable`, `visual`) all require an **actual loaded save/game** — they time out with `"blockingReason":"No current game is loaded."` if the game is only sitting at the main menu, which is a normal/expected ready state for most verification tasks.

Instead:
- Poll `rimbridge/get_bridge_status` and look for `state.programState == "Entry"` (main menu) or `state.hasCurrentGame == true` / `state.playable == true` (a loaded colony), with the log stream no longer advancing across polls (`latestLogSequence` stable) as the idle signal.
- Optionally confirm visually with `rimworld/take_screenshot`.

## Attention items

`games_call_tool` may be blocked by a pending attention item (`games_get_attention`/`games_ack_attention`). A recurring **benign** one on startup: repeated `Cannot create FMOD::Sound instance for clip ""` errors (empty audio clip path) — safe to acknowledge without further investigation. Other items, including ones tagged `severity: "fatal"`, should be read and judged on their own merits each time — check the surrounding `rimbridge/list_logs` output for whether startup completed normally afterward before assuming it's safe to dismiss; don't blanket-acknowledge everything.

## Driving the game

Use `games_tool_names` (with a `query` filter, e.g. `"mod"`, `"screenshot"`, `"debug"`) to discover relevant tools, `games_tool_detail` to inspect a specific tool's schema, then `games_call_tool` to invoke it. Large results (e.g. `rimworld/list_mods` with hundreds of installed mods) can exceed the context budget — a saved-to-file overflow includes a `jq`-based extraction hint; use it rather than dumping the whole payload.

## Verifying tests via a live run

RimTest Redux runs this project's test suite in-process on launch and writes its results to the log between two markers:

```
[RimTest Redux] TESTING START
...
[RimTest Redux] TESTING END
```

To verify a code change didn't break tests: launch the game (see above), wait for `TESTING END` to appear — either via GABS log tools (`rimbridge/list_logs`, or watch `latestLogSequence` advance) or by tailing `Player.log` under the project's `.savedatafolder/<version>/Player.log` — then stop the game (see "Stopping" below; the game will have reached the main menu by then, so it's safe to stop).

A fully successful run's `SUMMARY` line looks like:
```
[RimTest Redux] TESTING START
[RimTest Redux] SUMMARY
[RimTest Redux] [✓] ColonyManagerRedux.Tests || Test Suites : 26 ✓ || Tests : 245 ✓
[RimTest Redux] TESTING END
```
A failing run shows detailed per-test error output in place of clean ✓ counts — read that output rather than just checking for the markers' presence.

## Stopping

**Do not call `games_stop`/`games_kill` while RimWorld is still loading** (defs/mods still initializing) — it handles a stop signal badly mid-load and can hang or crash instead of exiting cleanly. Wait until the game has reached the main menu or a loaded game first (see "Waiting for the game to be ready" above).

`games_stop` can report `"stopped successfully"` even when the real game process didn't actually die — this happens when the game's `stopProcessName` isn't configured correctly in GABS (it should be `RimWorldLinux`; see this repo's `CLAUDE.md`). If in doubt, verify independently:

```bash
pgrep -af RimWorldLinux
```

If processes remain after `games_stop`/`games_kill`, escalate manually as a last resort — find every process in the tree (`flatpak-spawn`, both `bwrap` layers, and the real `RimWorldLinux` process) and `kill` them, retrying with `kill -9` for anything that survives plain `SIGTERM`.
