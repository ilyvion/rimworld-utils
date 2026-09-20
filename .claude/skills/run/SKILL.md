---
name: run
description: "Launch and drive this RimWorld mod project in a live game session via GABS/RimBridgeServer for AI-driven verification, screenshots, mod inspection, and debug-action testing. Use when asked to run, start, or screenshot the game, or to confirm a change works in the real game."
---

# Running a RimWorld mod project via GABS

**Stop — if the only goal here is running the unit test suite (not driving/inspecting a live game session), don't use this skill.** Read this repo's `CLAUDE.md` ("Writing tests" section) and its referenced `tests.md`/`gabs.md` files instead; `gabs.md` documents the test-verification procedure directly. This skill is for launching and driving the game itself via GABS.

This project is driven through **GABS** (a local MCP server) bridging to **RimBridgeServer** (the in-game GABP bridge), not by launching the game directly. This is a separate path from the project's VS Code F5 debug launch configs (`.vscode/launch.json`). The GABS entry's launcher is `gabs-launch.sh`, which runs `.vscode/build.sh <version>` itself before launching, so `games_start` always launches against a freshly built mod folder — no separate build step needed.

## Prerequisites

- The GABS tools (`mcp__gabs__*`) are deferred — load them first with `ToolSearch`, e.g. `select:mcp__gabs__games_list,mcp__gabs__games_status,mcp__gabs__games_start,mcp__gabs__games_connect,mcp__gabs__games_stop,mcp__gabs__games_get_attention,mcp__gabs__games_ack_attention,mcp__gabs__games_tool_names,mcp__gabs__games_tool_detail,mcp__gabs__games_call_tool`.
- The GABS `gameId` convention for these mod projects is **the project's own directory name** (e.g. a repo at `.../RimWorld/colony-manager-redux` registers as `gameId: colony-manager-redux`). Determine it from the current working directory's basename unless told otherwise. Confirm this project is registered: `games_list` should include this `gameId`. If it isn't, **stop and tell the user** — do not attempt to `gabs games add` it yourself; that requires interactive choices (executable paths, Steam library layout) the user needs to make. Point them at this repo's `CLAUDE.md` section "Launching RimWorld under GABS to run the test suite" for the setup steps (`gabs-launch.sh` wrapper, required `Working Directory`, required `stopProcessName: RimWorldLinux`). Each mod project needs its own GABS entry (own `gameId`, own `Working Directory`) — GABS config is per-project, though every project can point `Target` at the same shared `gabs-launch.sh`.
- There's no guarantee `brrainz.rimbridgeserver` itself is enabled in the target instance's `ModsConfig.xml`. Since this skill is for driving an actual GABP bridge session (not just launching and quitting), confirm `brrainz.rimbridgeserver` is present and enabled first, the same way as for RimTest Redux in `gabs.md`'s test-run procedure (DevTools projects need no test-framework mod in `ModsConfig.xml`; `brrainz.rimbridgeserver` is still needed here).

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

## Investigating a UI/layout bug efficiently

- **Don't speculate from a screenshot for long before touching the live game.** Static analysis of GUI layout math (Rects, scrollview content-height caching, one-frame lag between `EventType.Layout`/`Repaint`) is a rabbit hole — RimWorld's IMGUI layout is stateful enough that many plausible-looking theories turn out not to reproduce. If GABS is available and the bug is visual, prefer reproducing it live over reasoning about it from code for more than a couple of minutes.
- **`rimworld/get_ui_layout` responses are large** (every element in the window, recursively) and get expensive fast across many calls. Scope `surfaceId` to the specific window/dialog/float-menu you need (e.g. `window:<id>:<Type>`), not a broad default, and avoid re-fetching the full window layout when only one element's rect or the scrollview's `scroll` block changed.
- **UI element target IDs from `get_ui_layout` go stale the moment ANY window opens/closes** (a float menu appearing, a dialog closing) — even though the ID _looks_ like it should still refer to the same on-screen row. Re-capture layout immediately before every `click_ui_target` call that follows an intervening UI change; reusing an older capture's target ID silently clicks whatever now occupies that coordinate instead of erroring, which can burn several calls before the mistake is visible (e.g. adding a modifier to the wrong task because a stale row index pointed at a different row after the task list re-sorted/re-rendered).
- **Check a tool's schema with `games_tool_detail` before guessing parameter names** (e.g. `rimworld/scroll_ui_target` takes `targetX`/`targetY`, not `offsetX`/`offsetY`) — a failed call plus the follow-up correct one costs more than one detail lookup.
- **Reproduce with the smallest change that can trigger the bug** before building out a full realistic scenario. For a scrollview-bottom-padding bug, the fix was verifiable by adding just enough tasks to overflow the default window height and scrolling to the bottom — there was no need to replicate the exact task set/values from the original screenshot.
- **A rebuild+restart cycle to verify a one-line fix is expensive** (`games_stop` → `games_start` → `games_connect` → `rimworld/load_game` → `wait_for_game_loaded`, then re-driving the UI back to the same test state, since in-memory task list edits aren't persisted unless you explicitly save). Before restarting, consider whether it is truly necessary; if a restart is unavoidable, keep the pre-fix repro steps minimal so replaying them post-fix is cheap.

## Stopping

**Do not call `games_stop`/`games_kill` while RimWorld is still loading** (defs/mods still initializing) — it handles a stop signal badly mid-load and can hang or crash instead of exiting cleanly. Wait until the game has reached the main menu or a loaded game first (see "Waiting for the game to be ready" above).

`games_stop` can report `"stopped successfully"` even when the real game process didn't actually die — this happens when the game's `stopProcessName` isn't configured correctly in GABS (it should be `RimWorldLinux`; see this repo's `CLAUDE.md`). If in doubt, verify independently:

```bash
pgrep -af RimWorldLinux
```

If processes remain after `games_stop`/`games_kill`, escalate manually as a last resort — find every process in the tree (`flatpak-spawn`, both `bwrap` layers, and the real `RimWorldLinux` process) and `kill` them, retrying with `kill -9` for anything that survives plain `SIGTERM`.
