RimBridgeServer (the in-game GABP bridge) can be driven by an MCP client through **GABS** (`gabs` CLI/MCP server). This file covers launching the game specifically to run the mod's unit test suite (via RimTest Redux). For driving/inspecting a live game session — screenshots, mod inspection, UI investigation, debug-action testing — use the `run` skill instead; it covers the general launch/connect/attention/stop mechanics this file builds on (project registration, `gameId`/`Working Directory` setup, the GABP reconnect quirk, attention items, safely stopping).

## Which framework?

The procedure below is for projects using **RimTest Redux** (the `<ModName>.Tests` project references `RimTestRedux.csproj`). For projects using **DevTools** (it references `DevTools.csproj`), skip to [Running the tests with DevTools](#running-the-tests-with-devtools).

## Before launching

**Confirm RimTest Redux is present and enabled in the target instance's `ModsConfig.xml`** (e.g. `.savedatafolder/<version>/Config/ModsConfig.xml`), alongside the mod under test itself. Launching with RimTest Redux missing from the active mod list means `[RimTest Redux] TESTING START`/`TESTING END` will never appear (nothing runs the tests), and has been observed to corrupt/reset `ModsConfig.xml` on that launch, losing the previously active mod list. Check the file (or ask the user) before starting the game rather than assuming the last-known mod list is still correct.

The launcher is `gabs-launch.sh`, which runs `.vscode/build.sh <version>` itself before launching, so the mod folder the game loads from is always up to date for the version being launched — no separate build step needed.

## Running the tests

1. Read the `run` skill's "Prerequisites"/"Launching" steps and follow them directly — do not invoke the `run` skill itself to do this.
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

## Running the tests with DevTools

**Filter runs.** While implementing, always run DevTools tests filtered (`testFilter`, e.g. `class == FooTests` or `method == Bar`) to save time. Run the full unfiltered suite only once, as the final regression run before considering an implementation done.

Full detail (config, output format, gotchas) is in [devtools.md](devtools.md) ("Verified workflow"). Summary:

1. **Prerequisites.** The mod under test must be enabled in the instance's `ModsConfig.xml`; DevTools is not a mod and needs no entry (`DevTools.dll` loads from the mod's own `1.6/Assemblies`). The GABS game config must declare the `runTests` (and optionally `testFilter`) launch inputs, and GABS must be >= 1.1.0 (see devtools.md for the snippet). Only RimWorld 1.6 supports DevTools.
2. **Start the run.** Call the GABS MCP `games_start` tool with `launchInputs`:
   ```
   games_start {"gameId": "<game-id>", "launchInputs": {"runTests": true}}
   games_start {"gameId": "<game-id>", "launchInputs": {"runTests": true, "testFilter": "class == WithCountTests"}}
   ```
   The result lists `appliedLaunchInputs`; if it doesn't include `runTests`, the input isn't declared in the GABS game config (check with `games_show`). The result code `started_bridge_pending` is expected, since there is no bridge to attach to. `gabs-launch.sh` builds the mod, adds `--pid <packageId>`, writes `Configs/UnitTestConfig.xml`, and launches. The game **quits by itself** at the end of the run; there is nothing to stop afterwards.
3. **Wait for the end line in `Test.log`.** DevTools writes to `.savedatafolder/<version>/Test.log`, not `Player.log`, and has no `TESTING START/END` markers; `--exit` makes its last line `Test runner finished. Result: Passed|Failed`. `gabs-launch.sh` deletes the previous `Test.log` before launching the game, and `games_start` only returns once the game is running, so the file you wait on is always the new run's. Use a backgrounded shell wait (`run_in_background: true`) that notifies once on match, no sleeps or timeouts:
   ```
   until grep -q "Test runner finished. Result:" ".savedatafolder/<version>/Test.log" 2>/dev/null; do sleep 2; done
   ```
   If the run crashes or aborts, the line never appears; if the wait seems stuck, check `games_status` (game no longer running and no end line means the run aborted; see `Player.log`).
4. **Read the result** from `.savedatafolder/<version>/Test.log`: the last line is `Test runner finished. Result: Passed` or `Result: Failed`. Count tests with `grep -cP '^--\t\[Passed\]' Test.log` and `grep -cP '^--\t\[Failed\]' Test.log`; failure details follow each `[Failed]` line. A missing `Test.log`, or no `Test runner finished` line, means the run did not complete (check `Player.log` for `Unable to register` errors). A logged warning/error during a test fails it; see devtools.md's whitelisting section.
5. Tests run in the same game process as normal play, so do not use `runTests` when the game must stay running for interactive verification; start it normally (without the input) for that.

**Test plans**: declared the same way as `runTests`, via a `launchInputs` entry that maps to `--test-plan`/`-p`, e.g.:
```json
"testPlan": {
  "description": "Run a DevTools test plan (TestPlans/<name>.xml), then quit the game",
  "type": "string",
  "pattern": "[A-Za-z0-9_.-]+",
  "args": ["--test-plan", "${value}", "--exit"]
}
```
started with `games_start {"gameId": "<game-id>", "launchInputs": {"testPlan": "<plan-name>"}}`. A plan spawns a second child game process per job (`TestPlans/<name>.xml`'s `jobs`); see devtools.md's "Gotchas" `--test-plan` bullet for the child's launch args, per-child log naming (`Player-child<N>.log`), and why the parent and all its children safely append into one shared `Test.log` instead of clobbering it. **Wait on a different marker than step 3**: a job can itself run a nested `--test --exit` suite, so a multi-job plan's `Test.log` can contain more than one ordinary `Test runner finished. Result:` line before the plan is actually done. The parent writes its own `Test plan finished. Result: Passed|Failed` line once, after every job has finished, so use `until grep -q "Test plan finished. Result:" ".savedatafolder/<version>/Test.log" 2>/dev/null; do sleep 2; done` for plans instead.
