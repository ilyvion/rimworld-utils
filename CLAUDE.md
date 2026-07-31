# CLAUDE.md

> This file is likely a symlink shared across sibling mod repos (its canonical home is `rimworld-utils/CLAUDE.md`). If you need to edit it, `readlink -f CLAUDE.md` first and write to the resolved target — writing through the symlink path directly will be refused. This also means that any linked .md files in this file need to be resolved relative to the resolved target, not the symlink.

## Sibling repositories this project depends on

This repo lives alongside two sibling repositories under `RimWorld/` (i.e. at `../ilyvion.Laboratory` and `../rimworld-utils` relative to this repo). Both are the user's own repos and their source is available locally for reference when tracing a type/API that isn't defined anywhere in this repo.

- **`ilyvion.Laboratory`** (`../ilyvion.Laboratory`) — a RimWorld library/framework mod (namespace `ilyvion.Laboratory`) that some of the user's other RimWorld mods depend on to avoid code duplication. It's referenced as a project/assembly dependency (see `../rimworld-utils/Common.props`, referenced by `./Directory.Build.props` when `<UseLaboratory>true</UseLaboratory>` is present within. If `UseLaboratory` is not set, it is not available. It contains things like `CachedValue<T>`and`CachedValues<TKey, TValue>`(simple TTL-based caches, see`ilyvion.Laboratory/Cache.cs`), `MultiTickCoroutines`/`Coroutine`helpers,`Boxed<T>`, and other small shared utilities used throughout this codebase are defined there, not in this repo. If a type is used here but its definition can't be found in `Source/`, check there first before assuming it's from RimWorld itself.

- **`rimworld-utils`** (`../rimworld-utils`) — shared build tooling (not a mod). Provides `Common.props`/`Common.targets`, imported by this repo's `Directory.Build.props`, plus shared scripts (`build.sh`, `bump_version.sh`, `generate_refs.sh`, `steam_comment_extractor.py`, `html_to_steam.py`) and shared MSBuild/editorconfig conventions reused across the user's RimWorld mod projects. Check here when a build property, target, or script referenced by this repo's `.props`/`.targets` files or CI isn't defined locally.

## Global usings — why files don't `using Verse;`/`using RimWorld;`/`using UnityEngine;`

Every project in this repo family imports `../rimworld-utils/Common.props`, which compiles in `../rimworld-utils/globalusings.cs` (via a `<Compile Include>` item — not a project-local `GlobalUsings.cs`, and not the same thing as the generated `obj/**/*.GlobalUsings.g.cs` files that `ImplicitUsings` produces, which only cover base `System.*` namespaces and are a red herring here). `globalusings.cs` declares `global using` for `System.Collections`, `System.Globalization`, `System.Reflection`, `System.Runtime.CompilerServices`, `HarmonyLib`, `RimWorld`, `UnityEngine`, and `Verse` (plus `ilyvion.Laboratory`/`ilyvion.Laboratory.Coroutines`/a `Coroutine` alias when `USE_LABORATORY` is defined). That's why any `.cs` file in these mods can reference types like `Dialog_Confirm`, `Find`, `Widgets`, `Rect`, etc. with no explicit `using` statement.

## RimWorld game installation

The installed game (Steam/Flatpak) can be found at:

- Managed assemblies (e.g. `Assembly-CSharp.dll`): `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed`
- Game data (defs, textures, etc.): `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/Data`

For inspecting assemblies from older versions of Rimworld, there's also these directories:

- 1.3: `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed_1.3`
- 1.4: `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed_1.4`
- 1.5: `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed_1.5`

## Generating UI icons

- [Generating UI icons](icons.md) — Instructions for generating UI icons

## Translations

The mods tend to use Rimworld's translation system so texts aren't hard coded in any given language. When multiple languages are present in a project (which is rare, but it happens), when adding any new text, it must be added in every language present, not just for English.

## Launching RimWorld under GABS for AI-driven bridge sessions and test running

- [Launching RimWorld under GABS for AI-driven bridge sessions and test running](gabs.md) — Instructions for running RimWorld under GABS

## Writing tests

- [Writing tests](tests.md) — Instructions for writing tests; for actually running them, see the GABS section above

## Verifying a build succeeds

- "The build succeeds" means 0 warnings and 0 errors. Don't report success and leave warnings for the user to clean up — fix them as part of the change that introduced them.
- A project supports every RimWorld version listed in its `About/About.xml` `<supportedVersions>` (matching `LoadFolders.xml`'s `<vX.Y>` entries). Build with `.vscode/build.sh <version>` (see above) for _each_ supported version, not just the default/latest — a change can compile cleanly against the latest version's API while breaking an older one.

## CHANGELOG.md

Do not use quotation marks ("") in CHANGELOG.md. If you need to quote something, use apostrophes, ('').
