# CLAUDE.md

> This file is likely a symlink shared across sibling mod repos (its canonical home is `rimworld-utils/CLAUDE.md`). If you need to edit it, `readlink -f CLAUDE.md` first and write to the resolved target — writing through the symlink path directly will be refused.

## Sibling repositories this project depends on

This repo lives alongside two sibling repositories under `RimWorld/` (i.e. at `../ilyvion.Laboratory` and `../rimworld-utils` relative to this repo). Both are the user's own repos and their source is available locally for reference when tracing a type/API that isn't defined anywhere in this repo.

- **`ilyvion.Laboratory`** (`../ilyvion.Laboratory`) — a RimWorld library/framework mod (namespace `ilyvion.Laboratory`) that this and the user's other RimWorld mods depend on to avoid code duplication. It's referenced as a project/assembly dependency (see `Source/*/*.csproj` / build output `0ilyvion.Laboratory.dll`). Things like `CachedValue<T>` and `CachedValues<TKey, TValue>` (simple TTL-based caches, see `ilyvion.Laboratory/Cache.cs`), `MultiTickCoroutines`/`Coroutine` helpers, `Boxed<T>`, and other small shared utilities used throughout this codebase are defined there, not in this repo. If a type is used here but its definition can't be found in `Source/`, check there first before assuming it's from RimWorld itself.

- **`rimworld-utils`** (`../rimworld-utils`) — shared build tooling (not a mod). Provides `Common.props`/`Common.targets`, imported by this repo's `Directory.Build.props`, plus shared scripts (`build.sh`, `bump_version.sh`, `generate_refs.sh`, `steam_comment_extractor.py`, `html_to_steam.py`) and shared MSBuild/editorconfig conventions reused across the user's RimWorld mod projects. Check here when a build property, target, or script referenced by this repo's `.props`/`.targets` files or CI isn't defined locally.

## Global usings — why files don't `using Verse;`/`using RimWorld;`/`using UnityEngine;`

Every project in this repo family imports `../rimworld-utils/Common.props`, which compiles in `../rimworld-utils/globalusings.cs` (via a `<Compile Include>` item — not a project-local `GlobalUsings.cs`, and not the same thing as the generated `obj/**/*.GlobalUsings.g.cs` files that `ImplicitUsings` produces, which only cover base `System.*` namespaces and are a red herring here). `globalusings.cs` declares `global using` for `System.Collections`, `System.Globalization`, `System.Reflection`, `System.Runtime.CompilerServices`, `HarmonyLib`, `RimWorld`, `UnityEngine`, and `Verse` (plus `ilyvion.Laboratory`/`ilyvion.Laboratory.Coroutines`/a `Coroutine` alias when `USE_LABORATORY` is defined). That's why any `.cs` file in these mods can reference types like `Dialog_Confirm`, `Find`, `Widgets`, `Rect`, etc. with no explicit `using` statement.

## RimWorld game installation

The installed game (Steam/Flatpak) can be found at:

- Managed assemblies (e.g. `Assembly-CSharp.dll`): `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed`
- Game data (defs, textures, etc.): `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/Data`

For inspecting assemblies from older versions of Rimworld, there's also these directories:

- 1.4: `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed_1.4`
- 1.5: `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed_1.5`

## Generating UI icons

None of these mods ship hand-drawn icon assets — new UI icons (toolbar buttons, status indicators, chevrons, etc.) are generated via an SVG → PNG pipeline. This was established in RimTest's `Common/Textures/UI/` (see `RTRIconChevronCollapsed.svg`-style sources and `Source/RimTestRedux/Core/Icons.cs`/`StatusStyle.cs` for the resulting texture fields and color mapping), but the workflow applies to any mod in this family:

1. Write a source SVG at 64x64 (`viewBox="0 0 64 64"`), simple shapes (`polyline`, `path`, etc.) on a transparent background.
    - Neutral action/navigation icons (run, search, collapse/expand, chevrons — anything that isn't a status indicator) use a neutral stroke/fill color matching the game's UI text color (e.g. `#E4E1D8`).
    - Chevrons/arrows use `stroke-linecap="round"` (and `stroke-linejoin="round"`) for a softer look; `stroke-width="8"` at this viewBox reads well at in-game sizes.
    - Status icons (pass/fail/skip/unknown, etc.) use color-coded fills instead of the neutral color, matching whatever semantic color palette the mod already defines for that status.
2. Rasterize with ImageMagick: `magick -background none -density 384 <icon>.svg -resize 64x64 <icon>.png`. The high `-density` before downscaling gives clean anti-aliased edges instead of a blocky rasterization.
3. Copy the resulting PNG into the mod's `Common/Textures/UI/` (or equivalent) directory, then add a `ContentFinder<Texture2D>.Get("UI/IconName")` field for it wherever that mod centralizes its texture references (e.g. `Icons.cs`).

**Naming**: RimWorld's paths are a shared namespace across every installed mod, so a generic name like `icon_log.png` risks colliding with another mod's asset. Prefix filenames with a short per-mod code (RimTest Redux uses `RTR`) and use RimWorld's own `PascalCase` convention instead of `snake_case` — e.g. `RTRIconLog`, `RTRIconStatusPass`, `RTRIconChevronCollapsed`.

## Launching RimWorld under GABS for AI-driven bridge sessions

RimBridgeServer (the in-game GABP bridge) can be driven by an MCP client through **GABS** (`gabs` CLI/MCP server). This is a separate, parallel launch path — it does not replace or touch the VS Code F5 debug configs, and does not run the mod build step (`preLaunchTask`) those configs use, so build the mod yourself first if needed.

- **Build with `.vscode/build.sh <version>` (e.g. `.vscode/build.sh 1.6`), never a bare `dotnet build`.** `dotnet build` only produces compiler output under `Source/*/bin`; it does not touch the actual RimWorld mod folder the game loads from (`~/.var/app/.../Steam/steamapps/common/RimWorld/Mods/<ModName>` by default, or `$TARGET` from `build_config.sh`). `build.sh` runs `dotnet build` and then wipes and repopulates that mod folder from the build output plus `About/`, `Common/`, `LoadFolders.xml`, and any configured extra files — skip it and GABS will launch the game against stale or missing mod files, silently invalidating any verification.
- The launcher is `gabs-launch.sh`.
- Each mod project needs its own GABS entry (own `gameId`, own `Working Directory`) — GABS config is per-project, though every project can point `Target` at the same shared `gabs-launch.sh`.
- The initial GABP connection sometimes drops once, early during game startup (`games_call_tool` errors with "GABP client connection closed"); `games_status` will show `running-disconnected` — just call `games_connect` again, no need to restart the game.
- **Do not call `games_stop`/`games_kill` while RimWorld is still loading** (defs/mods still initializing) — RimWorld handles a stop signal badly mid-load and can hang or crash instead of exiting cleanly, and `games_stop` will still report "stopped successfully" even when the real process didn't die (verify independently with `pgrep -af RimWorldLinux | grep -v pgrep` if it matters). Wait until it's reached the main menu or a loaded game first. There's no dedicated "wait for main menu" tool — `rimbridge/wait_for_game_loaded`'s readiness targets (`gameData`, `mapData`, `currentMap`, `playable`, `visual`) all require an actual loaded save/game and time out at the main menu with `"blockingReason":"No current game is loaded."`. Instead, poll `rimbridge/get_bridge_status` and look for `state.programState == "Entry"` with the log stream idle (`latestLogSequence` no longer advancing across polls).
- A recurring benign `games_get_attention` item on startup: repeated `Cannot create FMOD::Sound instance for clip ""` errors (empty audio clip path) — safe to acknowledge via `games_ack_attention` without further investigation. A `severity: "fatal"` entry alongside it (e.g. a `NullReferenceException`) during early startup has so far not been blocking either, if the log stream goes on to show a normal successful startup afterward — but treat that judgment call skeptically per session, don't assume it's always safe to ignore.
- An agent can verify a mod's tests actually pass by launching the game this way and watching for RimTest Redux's test-run markers in the log, between `[RimTest Redux] TESTING START` and `[RimTest Redux] TESTING END` (via GABS log tools, or by tailing `Player.log` under the project's `.savedatafolder/<version>/Player.log`), then stopping the game once `TESTING END` appears. A fully successful run looks like:
    ```
    [RimTest Redux] TESTING START
    [RimTest Redux] SUMMARY
    [RimTest Redux] [✓] ColonyManagerRedux.Tests || Test Suites : 26 ✓ || Tests : 245 ✓
    [RimTest Redux] TESTING END
    ```
    A failing run shows detailed per-test error output in place of clean ✓ counts — read that output rather than just checking for the markers' presence.

## Writing tests

Tests for a mod live in a sibling `<ModName>.Tests` project (e.g. `Source/ColonyManagerRedux.Tests`) and run in-process, in-game, via the sibling **RimTest Redux** mod (`../RimTest`, project `RimTestRedux.csproj`). This project is dev-only:

- It should be listed in the mod's `.sln` so it always builds locally (default `Debug` config), but the `.sln`'s `ProjectConfigurationPlatforms` section shoul deliberately omit a `Release|Any CPU.Build.0` line for its GUID, so a `Release` build (what CI/release always uses) skips it entirely and its output never ships. Don't "fix" this by adding the missing line.
- **Only after adding or moving projects/references**, verify the split still holds: delete the built `<ModName>.Tests.dll`, build `Release` and confirm it stays absent, then rebuild `Debug` to restore normal dev state.

**Test suite mechanics**: a suite is a `static class` marked `[TestSuite]`; each test is a `static void` parameterless method marked `[Test]`. Assertions use a fluent API with interchangeable no-op grammar links (`.Is`, `.Not`, `.Has`, `.Does`, etc.) — `Assert.That(IComparable)` for scalars, `Assert.ThatCollection(IEnumerable)` for collections (real methods include `Has.Count(int)`, `Does.Contain(object)`, `Is.Empty()` — don't guess at the API; grep `RimTestRedux/Testing/Assert.cs` in the sibling repo for the real surface before relying on an assertion you haven't seen used elsewhere in the mod).

To unit test logic that's entangled with game/presentation logic:

1. Extract the pure, side-effect-free branching/comparison logic out of the method into its own `internal static` method (generic over plain types/delegates instead of live game types where possible), leaving the original call site to call it.
2. Grant the test project visibility: add `[assembly: InternalsVisibleTo("<ModName>.Tests")]` to the project the logic lives in (see existing `Core/InternalsVisibleTo.cs`-style files for the pattern) and add that project as a `ProjectReference` in the `.Tests.csproj` if it isn't already there.
3. Before writing assertions against an actual RimWorld/Verse type's constructor or API shape, verify it rather than guessing — e.g. inspect `~/.nuget/packages/krafs.rimworld.ref/<version>/ref/net472/Assembly-CSharp.dll` via reflection metadata, or grep the mod/siblings for existing working usages.

Prefer test names and bodies that state the behavior being guarded (e.g. `FailedSwapRestoresOriginalContents`), and where a test exists specifically because of a past bug, say so in a comment so a future change doesn't silently regress it.

Every code change must be accompanied by one or more tests. If the code being modified has no existing test coverage, write a passing test that captures the current behavior _before_ making any changes — this acts as a regression guard. Then make the change and add or update tests to cover the new behavior.
