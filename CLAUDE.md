# CLAUDE.md

> This file is likely a symlink shared across sibling mod repos (its canonical home is `rimworld-utils/CLAUDE.md`). If you need to edit it, `readlink -f CLAUDE.md` first and write to the resolved target — writing through the symlink path directly will be refused.
>
> Every other `.md` file linked from this one (`icons.md`, `gabs.md`, `tests.md`, etc.) lives only in that same canonical directory — they are **not** individually symlinked into this repo, so they will not exist at their plain relative path here (e.g. `gabs.md` in this repo's own root) and running `readlink` or `ls` on them here will just fail. Do not try to resolve each linked file separately. Instead, resolve the directory once — `dirname "$(readlink -f CLAUDE.md)"` — and read every linked `.md` from that directory. If `readlink -f CLAUDE.md` prints this same repo's own path, CLAUDE.md is the canonical copy, not a symlink, and every linked file already sits right next to it — read them directly, no resolution needed.
>
> Some repos don't symlink `CLAUDE.md` itself; instead their `CLAUDE.md` is a real, repo-specific file that pulls this shared content in via an `@CLAUDE.SHARED.md` include, where `CLAUDE.SHARED.md` (not `CLAUDE.md`) is the symlink to this canonical file. The harness inlines `@`-includes before you see them, so this shared content can show up even when `readlink -f CLAUDE.md` resolves to that repo's own path — that result alone does not mean everything is local. Before trusting a same-path result, `ls` the same directory for a `CLAUDE.SHARED.md` (or similarly named symlinked include); if one exists, `readlink -f` _that_ file instead to find the real canonical directory to read linked `.md` files from.

## Sibling repositories this project depends on

This repo lives alongside two sibling repositories under `RimWorld/` (i.e. at `../ilyvion.Laboratory` and `../rimworld-utils` relative to this repo). Both are the user's own repos and their source is available locally for reference when tracing a type/API that isn't defined anywhere in this repo.

- **`ilyvion.Laboratory`** (`../ilyvion.Laboratory`) — a RimWorld library/framework mod (namespace `ilyvion.Laboratory`) that some of the user's other RimWorld mods depend on to avoid code duplication. It's referenced as a project/assembly dependency (see `../rimworld-utils/Common.props`, referenced by `./Directory.Build.props` when `<UseLaboratory>true</UseLaboratory>` is present within. If `UseLaboratory` is not set, it is not available. It contains things like `CachedValue<T>`and`CachedValues<TKey, TValue>`(simple TTL-based caches, see`ilyvion.Laboratory/Cache.cs`), `MultiTickCoroutines`/`Coroutine`helpers,`Boxed<T>`, and other small shared utilities used throughout this codebase are defined there, not in this repo. If a type is used here but its definition can't be found in `Source/`, check there first before assuming it's from RimWorld itself.

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

Every code change must be accompanied by one or more tests insofar as it is possible to test the code. If the code being modified has no existing test coverage, write a passing test that captures the current behavior _before_ making any changes — this acts as a regression guard. Then make the change and add or update tests to cover the new behavior.

- [Writing tests](tests.md) — Instructions for writing tests; for actually running them, see the GABS section above

## Verifying a build succeeds

- "The build succeeds" means 0 warnings and 0 errors. Don't report success and leave warnings for the user to clean up — fix them as part of the change that introduced them.
- A project supports every RimWorld version listed in its `About/About.xml` `<supportedVersions>` (matching `LoadFolders.xml`'s `<vX.Y>` entries). Build with `.vscode/build.sh <version>` (see above) for _each_ supported version, not just the default/latest — a change can compile cleanly against the latest version's API while breaking an older one.

## CHANGELOG.md

Do not use quotation marks ("...") in CHANGELOG.md. If you need to quote something, use apostrophes, ('...').

The order in the changelog, per release, is Dependencies, Added, Changed, Fixed.

Any new entry added to the change log goes at the bottom of its section.
