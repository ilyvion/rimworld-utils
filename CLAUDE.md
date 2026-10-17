# CLAUDE.md

> This file is likely a symlink shared across sibling mod repos (its canonical home is `rimworld-utils/CLAUDE.md`). If you need to edit it, `readlink -f CLAUDE.md` first and write to the resolved target — writing through the symlink path directly will be refused.
>
> Every other `.md` file linked from this one (`icons.md`, `gabs.md`, `tests.md`, etc.) lives only in that same canonical directory — they are **not** individually symlinked into this repo, so they will not exist at their plain relative path here (e.g. `gabs.md` in this repo's own root) and running `readlink` or `ls` on them here will just fail. Do not try to resolve each linked file separately. Instead, resolve the directory once — `dirname "$(readlink -f CLAUDE.md)"` — and read every linked `.md` from that directory. If `readlink -f CLAUDE.md` prints this same repo's own path, CLAUDE.md is the canonical copy, not a symlink, and every linked file already sits right next to it — read them directly, no resolution needed.
>
> Some repos don't symlink `CLAUDE.md` itself; instead their `CLAUDE.md` is a real, repo-specific file that pulls this shared content in via an `@CLAUDE.SHARED.md` include, where `CLAUDE.SHARED.md` (not `CLAUDE.md`) is the symlink to this canonical file. The harness inlines `@`-includes before you see them, so this shared content can show up even when `readlink -f CLAUDE.md` resolves to that repo's own path — that result alone does not mean everything is local. Before trusting a same-path result, `ls` the same directory for a `CLAUDE.SHARED.md` (or similarly named symlinked include); if one exists, `readlink -f` _that_ file instead to find the real canonical directory to read linked `.md` files from.

## Sibling repositories this project depends on

This repo lives alongside two sibling repositories under `RimWorld/` (i.e. at `../ilyvion.Laboratory` and `../rimworld-utils` relative to this repo). Both are the user's own repos and their source is available locally for reference when tracing a type/API that isn't defined anywhere in this repo.

- **`ilyvion.Laboratory`** (`../ilyvion.Laboratory`) — a RimWorld library/framework mod (namespace `ilyvion.Laboratory`) that some of the user's other RimWorld mods depend on to avoid code duplication. It's referenced as a project/assembly dependency (see `../rimworld-utils/Common.props`, referenced by `./Directory.Build.props`) when `<UseLaboratory>true</UseLaboratory>` is present. If `UseLaboratory` is not set, it is not available. It contains things like `CachedValue<T>` and `CachedValues<TKey, TValue>` (simple TTL-based caches, see `ilyvion.Laboratory/Cache.cs`), `MultiTickCoroutines`/`Coroutine` helpers, `Boxed<T>`, and other small shared utilities used throughout this codebase are defined there, not in this repo. If a type is used here but its definition can't be found in `Source/`, check there first before assuming it's from RimWorld itself.

- **`rimworld-utils`** (`../rimworld-utils`) — shared build tooling (not a mod). Provides `Common.props`/`Common.targets`, imported by this repo's `Directory.Build.props`, plus shared scripts (`build.sh`, `bump_version.sh`, `generate_refs.sh`, `steam_comment_extractor.py`, `html_to_steam.py`) and shared MSBuild/editorconfig conventions reused across the user's RimWorld mod projects. Check here when a build property, target, or script referenced by this repo's `.props`/`.targets` files or CI isn't defined locally.

## Accessing private/internal members of referenced assemblies: Krafs.Publicizer

`Common.props` references `Krafs.Publicizer`, so it's available in every project in this repo family — never fall back to manual reflection (`AccessTools.TypeByName`/`Field`/`Method`, `System.Reflection`) to reach a private or internal member of a _referenced assembly_ (RimWorld itself, another mod being integrated with, etc.). Instead, add a `Publicize` item to that project's own `.csproj`:

```xml
<ItemGroup>
    <Publicize Include="AssemblyName:Namespace.Type.Member" />
    <!-- nested types use +, .NET's own separator, not another dot: Namespace.Outer+Inner -->
    <Publicize Include="AssemblyName:Namespace.Outer+Inner.Field" />
</ItemGroup>
```

This rewrites the referenced assembly's access modifiers to public at compile time (and suppresses the runtime's `MemberAccessException`), so the member can then be used directly and with full static typing (`typeof(...)`, `nameof(...)`, normal member access) instead of through string-based reflection lookups. Existing examples: `Source/RealisticOrbitalTrade/RealisticOrbitalTrade.csproj`, `Source/RealisticOrbitalTrade.DynamicTradeInterface/RealisticOrbitalTrade.DynamicTradeInterface.csproj`.

Reflection is still the right tool for members that don't exist at compile time at all (e.g. truly dynamic/reflection-based APIs), but not as a workaround for accessibility.

## Other mods' reference assemblies (`.deps/`)

Integration projects reference another mod's assembly through `$(RimWorldSteamWorkshopFolderPath)\<workshop id>\$(RimWorldVersion)\Assemblies\<Name>.dll`. Locally and in CI that path is the repo's `.deps/refs`, which holds reference assemblies (made by refasmer) of the installed mods:

- `.deps/originals_config.sh` lists the workshop ids; `.deps/link_originals.sh` symlinks them into `.deps/originals/` and keeps `.deps/README.md` in sync; `.deps/generate_refs.sh` deletes and rebuilds `.deps/refs` from every DLL under `.deps/originals/`.
- The repo's `/**/Assemblies` ignore rule matches `.deps/refs`, so its tracked files were force-added. After regenerating, `git add -f` each new DLL a project's `Reference` points at (e.g. `.deps/refs/3020706506/1.7/Assemblies/DynamicTradeInterface.dll`); otherwise CI can't find it. Add only those: the tracked set leaves out the other DLLs and version folders refasmer produces, and new untracked folders it creates (other versions, `Source/` builds) get deleted.
- A `Reference` whose `$(RimWorldVersion)` folder doesn't exist resolves from `$(RimWorldFallbackVersion)` (`1.6` on 1.7 builds, see `Common.targets`), so a mod with no build for the newest version is compiled against its previous one.

## Global usings — why files don't `using Verse;`/`using RimWorld;`/`using UnityEngine;`

Every project in this repo family imports `../rimworld-utils/Common.props`, which compiles in `../rimworld-utils/globalusings.cs` (via a `<Compile Include>` item — not a project-local `GlobalUsings.cs`, and not the same thing as the generated `obj/**/*.GlobalUsings.g.cs` files that `ImplicitUsings` produces, which only cover base `System.*` namespaces and are a red herring here). `globalusings.cs` declares `global using` for `System.Collections`, `System.Globalization`, `System.Reflection`, `System.Runtime.CompilerServices`, `HarmonyLib`, `RimWorld`, `UnityEngine`, and `Verse` (plus `ilyvion.Laboratory`/`ilyvion.Laboratory.Coroutines`/a `Coroutine` alias when `USE_LABORATORY` is defined). That's why any `.cs` file in these mods can reference types like `Dialog_Confirm`, `Find`, `Widgets`, `Rect`, etc. with no explicit `using` statement.

**Do not investigate where a type comes from.** Missing `using` directives in a file are expected, not a mystery; don't grep `Common.props`, `globalusings.cs`, or the csproj to explain them. You don't need to know where a type comes from to use it. If a type needs a `using` that's missing, the compiler will say so at build time; there is no need to statically verify where everything comes from ahead of time.

## RimWorld game installation

The installed game (Steam/Flatpak) is RimWorld 1.7, found at:

- Assemblies (e.g. `Assembly-CSharp.dll`, `UnityStubs.dll`, `Waylith.dll`): directly in the game root, `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld`
- Game data (defs, textures, etc.): `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/Data`

Assemblies for older versions of RimWorld (1.3, 1.4, 1.5, 1.6) are in `/home/alex/Documents/Projects/RimWorld/rimworld-utils/Game Assemblies/<version>/`, e.g. `/home/alex/Documents/Projects/RimWorld/rimworld-utils/Game Assemblies/1.6/Assembly-CSharp.dll`. Use these, not the game installation, for anything specific to a version older than 1.7.

## RimWorld modding lore (`lore/`)

`lore/` (in the same canonical directory as this file) holds short, self-contained write-ups of RimWorld modding pitfalls and vanilla behavior. Don't read the whole directory. Before starting work in one of the areas below, read the matching file, or `grep '^## ' lore/<file>.md` for its section headings and read only the relevant sections. Also check it when an error, crash, log warning or unexpected in-game behavior in one of these areas shows up.

| File | Read when working on |
| --- | --- |
| `lore/build.md` | Compile errors (missing `using`, wrong namespace), locating the real name of a vanilla type, background-thread restrictions (mod constructor, `MapComponent.FinalizeInit`), `About.xml` load failures |
| `lore/harmony.md` | Writing or debugging any Harmony patch: overloads, `out`/`ref`, generics, optional-mod patches, prefix/postfix/`__result` handling, `TargetMethod`, publicizing for `nameof` |
| `lore/defs.md` | Authoring or patching Defs: `ThingDef`, `ScenarioDef`, `NeedDef`, comps, `colorGenerator`, def-name and label restrictions, `WorldComponent.FinalizeInit` |
| `lore/patches.md` | XML patch operations (`PatchOperationAdd`, XPath, `Patches/` file structure) |
| `lore/things.md` | Things, buildings, blueprints/frames, zones, hediffs, filth, coolers, custom caravans and world-map movers, `ThingComp` ticking, collection ownership |
| `lore/pawns.md` | Pawn names and traits, spawning/despawning, `WorldPawns`, finding pawns across maps, mutant/downed behavior |
| `lore/factions.md` | Faction generation, `FactionDef` fields, `requiredCountAtGameStart` |
| `lore/ideology.md` | Precepts, rituals, ritual quality factors |
| `lore/ui.md` | Any IMGUI/window/gizmo/float-menu/letter/scroll-view/text-metrics work; coordinate-space conversions; hotkeys; `GameComponent`-based state |
| `lore/sounds.md` | `SoundDef` authoring, one-shots, sustainers, camera sounds |
| `lore/textures.md`, `lore/assets.md` | Loading textures, graphics classes, asset paths, AssetBundles, "Could not load Texture2D/AudioClip" |
| `lore/animation.md` | Custom pawn/plant rendering, per-frame animation |
| `lore/weather.md` | Weather changes, silencing music from a `GameCondition` |
| `lore/world-incidents.md` | Starting `GameCondition`s, faction leader death |
| `lore/scribe-saving.md` | Saving/loading custom `WorldObject`s, static caches surviving load/exit-to-menu |
| `lore/performance.md` | Ticking cost, per-tick patches, pausing behavior of `MapComponent`, pawn-path pool warnings |
| `lore/localization.md` | Any user-facing string, Keyed/DefInjected translations |
| `lore/compat.md` | Handling conflicts with other mods' Harmony patches |
| `lore/test-loop.md` | Reading `Player.log` errors, time-advance debug actions in tests |
| `lore/misc.md` | 1.5 → 1.6 porting errors, `LookTargets`/`CellRect` default-value traps, second-camera rendering, stray right-click behavior |

Some entries carry a `> **Unverified:**` note. Verify those claims with the decompiler MCP or the vanilla defs before relying on them.

## Generating game art

- [RimWorld Art Style Guide](rimworld-art-style-guide.md) — Mandatory reference for any agent-generated game art (item icons, buildings, pawns/animals, plants, UI icons, terrain, effects) that needs to fit RimWorld's vanilla visual style and plug correctly into RimWorld's colouring system. Read it before drawing anything, not just when generating UI icons below.

## Generating UI icons

- [Generating UI icons](icons.md) — Instructions for generating UI icons; defers to the art style guide above for the actual visual style.

## Translations

The mods tend to use Rimworld's translation system so texts aren't hard coded in any given language. When multiple languages are present in a project (which is rare, but it happens), when adding any new text, it must be added in every language present, not just for English.

## Which test framework a project uses

Projects are migrating from RimTest Redux to DevTools (SmashPhil). Tell them apart by the `<ModName>.Tests` project's references: `RimTestRedux.csproj` (`using RimTestRedux;`, `[TestSuite]`) means **RimTest Redux**; `DevTools.csproj` (`using DevTools.Testing;`, `[TestFixture]`) means **DevTools**. Every test-related instruction below has a branch for each; follow the one matching the project.

- [DevTools reference](devtools.md) — DevTools features, test-writing API, RimTest Redux → DevTools translation table, result retrieval, gotchas. Read it before writing, converting or running DevTools tests.
- To convert a project from RimTest Redux to DevTools, use the `rimtest-to-devtools` skill.

## Launching RimWorld under GABS to run the test suite

- [Launching RimWorld under GABS to run the test suite](gabs.md) — Instructions for launching RimWorld under GABS to run the mod's unit tests (RimTest Redux and DevTools procedures). For full AI-driven game sessions (screenshots, mod inspection, UI investigation, debug-action testing), use the `run` skill instead.

## Writing tests

Every code change must be accompanied by one or more tests insofar as it is possible to test the code. If the code being modified has no existing test coverage, write a passing test that captures the current behavior _before_ making any changes — this acts as a regression guard. Then make the change and add or update tests to cover the new behavior.

- [Writing tests](tests.md) — Instructions for writing tests (RimTest Redux and DevTools); for actually running them, see the GABS section above

## Verifying a build succeeds

- Every code change must be verified by an actual build before the task is considered done. Skipping the build, or reasoning that the code "should compile", is not acceptable.
- "The build succeeds" means 0 warnings and 0 errors. Read the build output for warnings, not just errors. Don't report success and leave warnings for the user to clean up — fix them as part of the change that introduced them, then rebuild until the output is clean.
- Incremental builds can hide warnings from files that weren't recompiled, so the final verification build must be a clean build (e.g. `dotnet build --no-incremental`, or `dotnet clean` first) rather than an incremental one.
- A project supports every RimWorld version listed in its `About/About.xml` `<supportedVersions>` (matching `LoadFolders.xml`'s `<vX.Y>` entries). Build with `.vscode/build.sh <version>` for _each_ supported version, not just the default/latest — a change can compile cleanly against the latest version's API while breaking an older one.

## Formatting code

This repo family uses `csharpier` as its code formatter, not the IDE/editorconfig-driven formatting rules. Run `csharpier format .` after making edits and before considering a change done (e.g. as the formatting step of the `address-task` skill).

## CHANGELOG.md

Do not use quotation marks ("...") in CHANGELOG.md. If you need to quote something, use apostrophes, ('...').

The order in the changelog, per release, is Dependencies, Added, Changed, Fixed.

Any new entry added to the change log goes at the bottom of its section.
