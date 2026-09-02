RimBridgeServer (the in-game GABP bridge) can be driven by an MCP client through **GABS** (`gabs` CLI/MCP server). This file covers launching the game specifically to run the mod's unit test suite (via RimTest Redux). For driving/inspecting a live game session — screenshots, mod inspection, UI investigation, debug-action testing — use the `run` skill instead; it covers the general launch/connect/attention/stop mechanics this file builds on (project registration, `gameId`/`Working Directory` setup, the GABP reconnect quirk, attention items, safely stopping).

## Before launching

**Confirm RimTest Redux is present and enabled in the target instance's `ModsConfig.xml`** (e.g. `.savedatafolder/<version>/Config/ModsConfig.xml`), alongside the mod under test itself. Launching with RimTest Redux missing from the active mod list means `[RimTest Redux] TESTING START`/`TESTING END` will never appear (nothing runs the tests), and has been observed to corrupt/reset `ModsConfig.xml` on that launch, losing the previously active mod list. Check the file (or ask the user) before starting the game rather than assuming the last-known mod list is still correct.

The launcher is `gabs-launch.sh`, which runs `.vscode/build.sh <version>` itself before launching, so the mod folder the game loads from is always up to date for the version being launched — no separate build step needed.

## Running the tests

1. Launch the game following the `run` skill's "Prerequisites"/"Launching" steps.
2. RimTest Redux runs the test suite in-process and writes results to the log between two markers:
   ```
   [RimTest Redux] TESTING START
   ...
   [RimTest Redux] TESTING END
   ```
   RimWorld truncates `Player.log` itself on startup, so a stale `TESTING END` from a previous run cannot leak into a fresh one — do not manually delete/truncate the log before launching to "be safe"; that only races the game's own truncation.
3. **Wait for `TESTING END` without polling via `ScheduleWakeup`/sleep loops — they overshoot and waste time.** Since it's a one-shot terminal marker, use a backgrounded shell wait that blocks until it appears and notifies immediately (adjust the version path as needed):
   ```
   until grep -q "TESTING END" ".savedatafolder/<version>/Player.log" 2>/dev/null; do sleep 2; done
   ```
   Run via the Bash tool with `run_in_background: true` — this is the "tell me when X is ready" case, so a single notification on match beats any GABS log-tool polling loop too.
4. Read the log between the markers to determine pass/fail. A fully successful run's `SUMMARY` line looks like:
   ```
   [RimTest Redux] TESTING START
   [RimTest Redux] SUMMARY
   [RimTest Redux] [✓] ColonyManagerRedux.Tests || Test Suites : 26 ✓ || Tests : 245 ✓
   [RimTest Redux] TESTING END
   ```
   A failing run shows detailed per-test error output in place of clean ✓ counts — read that output rather than just checking for the markers' presence.
5. Stop the game following the `run` skill's "Stopping" section (the game will have reached the main menu by then, so it's safe to stop) — **unless the response to the user is going to ask them to (re-)verify something in-game themselves, in which case leave the game running instead of stopping it.** Telling the user to go verify in-game after stopping the game the agent just launched forces them to redo the launch and wait for the game to load work the agent already did. Only stop the game if the task is fully done and nothing further needs eyes-on confirmation, or if the user has said not to leave it running.
