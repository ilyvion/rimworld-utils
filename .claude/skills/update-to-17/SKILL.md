---
name: update-to-17
description: "Add RimWorld 1.7 support to a mod project that already builds for 1.6 with the shared rimworld-utils tooling: load folders, csproj conditions, test project, #if v1_7 code paths for CoreCLR/Waylith/HarmonyMod differences, and verification by building every supported version. Use when asked to add 1.7 support, port/convert/migrate a mod to RimWorld 1.7, or make a mod work on 1.7."
---

# Adding RimWorld 1.7 support to a mod

1.7 keeps 1.6 support: everything 1.7-specific goes behind a version condition, and both versions must still build and pass. Ask the user (AskUserQuestion) whenever something is unclear; don't guess. Do not commit unless told to.

**Reference port:** DebugAssistance (`../DebugAssistance`), commits `2a11fd4..ab5311f` (`git -C ../DebugAssistance log -p 66cd417..ab5311f`). Most of that diff is DebugAssistance's own error-capture code; the general lessons from it are distilled below. Look at it for the shape of `#if` branches, not for code to copy.

## What 1.7 is

- The game runs on **CoreCLR (.NET 10)**, not Mono. Mods compile as `net10.0`.
- The engine is **Waylith**, not Unity. `UnityEngine` types (`Rect`, `Vector3`, `GUI`, ...) still exist, as stand-ins in the `UnityStubs` assembly. The game's host executable is the `RimWorld` assembly, game code is still `Assembly-CSharp`, and engine code is in `Waylith*` assemblies.
- Harmony is Harmony 3 (mods compile against the newest `Lib.Harmony 3.0.x`, prereleases included), whose internals differ from 1.6's Harmony 2.

## What the shared tooling already does

`Common.props` handles 1.7 when `RimWorldVersion` is `1.7` (its default): `net10.0`, the `v1_7;v1_7_OR_GREATER;v1_6_OR_GREATER;...` defines, `CopyLocalLockFileAssemblies`, `Krafs.Rimworld.Ref 1.7.*-*` and `Lib.Harmony 3.0.*-*` (no `PolySharp` on 1.7). `.vscode/build.sh 1.7`, `gabs-launch.sh` (`rimworldVersion` launch input) and DevTools all support 1.7. Don't re-implement any of this in the mod; if something there is missing or wrong, tell the user, since it's shared across every mod.

## 1. Baseline

- `.vscode/build.sh <version>` for every version in `About/About.xml` `<supportedVersions>`: each must be 0 warnings, 0 errors before you start.
- `.vscode/build.sh 1.7` and keep the error list; it's your work list for steps 3-4.

## 2. Project files

- **`LoadFolders.xml`**: add a `<v1.7>` block mirroring `<v1.6>` with `1.6` replaced by `1.7` (e.g. `<li>Common</li><li>1.7</li>`). Keep any `IfModActive` integration entries, with their paths pointing at the `1.7` folders. If a version-specific content folder (`1.6/Defs`, `1.6/Patches`, ...) holds tracked content, ask the user whether 1.7 should share it (list the `1.6` folder in the `v1.7` block) or get its own copy.
- **`About/About.xml`**: add `<li>1.7</li>` to `<supportedVersions>`, after `1.6`.
- **GitHub workflows** (`.github/workflows/*.yml`): the shared `build_mod.yml` reads its versions from `<supportedVersions>`, so no change is needed. If they still pass a `rw_versions` list, delete it; the input is ignored.
- **Mod csproj**: NuGet packages that back-port BCL types to net48 (e.g. `System.Memory`) are part of .NET 10 and conflict with it; give them `Condition="'$(RimWorldVersion)' != '1.7'"`. Keep any comment on them accurate. Other packages (e.g. `Newtonsoft.Json`) stay.
- **ILRepack**: a mod that ILRepacks its dependencies and has a test project needs `<ProduceReferenceAssembly>false</ProduceReferenceAssembly>` in the mod csproj, since on net10.0 a `ProjectReference` compiles against the reference assembly, which lacks the merged types.
- **Test project** (if it uses DevTools; see `devtools.md`, "Project setup"). DevTools supports only 1.6 and 1.7, so the test project builds tests for exactly those two versions and stays an empty assembly for every other one:
    - The DevTools `ProjectReference`'s `ItemGroup` condition becomes `'$(RimWorldVersion)' == '1.6' Or '$(RimWorldVersion)' == '1.7'`, and the reference gets `AdditionalProperties="RimWorldVersion=$(RimWorldVersion);OutDir=$(MSBuildThisFileDirectory)..\..\$(RimWorldVersion)\Assemblies\"`.
    - Keep the `<Compile Remove="**/*.cs" />` group and the `CS0436` `NoWarn` group; pre-1.6 builds need them. Change their conditions from `'$(RimWorldVersion)' != '1.6'` to `'$(RimWorldVersion)' != '1.6' And '$(RimWorldVersion)' != '1.7'`.
    - Remove any `Lib.Harmony.Ref` `ExcludeAssets="all"` reference; DevTools keeps its Harmony reference private.
    - Update the csproj comments to match (no history narration).

## 3. Code: version conditions

Use `#if v1_7_OR_GREATER` (with `#else` for the 1.6 path) for runtime/engine differences, which will hold for later versions. Use `#if v1_7` (exact) only for things tied to this specific HarmonyMod/game build that a later version may change again. Put a one-line comment on each 1.7 branch stating what is different on 1.7. Prefer a small helper with the `#if` inside (one place) over `#if` blocks scattered across call sites.

Fix compile errors first (they're listed by the step-1 1.7 build), then go through the runtime differences below: most of them compile fine and fail only in the game.

### Compile-time differences

- **Reflection.Emit save**: `AppDomain.DefineDynamicAssembly(..., AssemblyBuilderAccess.Save/RunAndSave, dir)` and `DefineDynamicModule(name, fileName)` don't exist. On 1.7 use `new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly)`, `DefineDynamicModule(name)`, and `Save(fullPath)` (a full path, not a file name).
- **Removed or changed game/engine APIs**: look the member up in the `rw17` context (game) or `unitystubs17`/`waylith17` (engine) and compare with `rw16` (see "Investigating with the decompiler").
- **Warnings that appear only because of the switch to 1.7** (new analyzer warnings on net10.0, obsoletions, ...): leave them alone for now and only report how many there are; they'll be addressed once 1.7 is officially released. Warnings on any other version, and warnings in code you added for 1.7, are still yours to fix.

### Runtime differences (compile fine, break in game)

- **Patch parameter names**: Harmony binds patch method parameters to the target's parameters by name, so a target whose parameters were renamed on 1.7 breaks the patch at patch time even when the types match. Rename the patch's parameters to match under `#if`.
- **Inlining**: the CoreCLR JIT inlines small private methods into their callers, so a patch on such a method can silently never run. Patch a public or virtual entry point instead, and list every patch on a small method for the user to confirm in game.
- **A patch whose target is no longer called on 1.7**: add `private static bool Prepare() => false;` under `#if v1_7_OR_GREATER` (Harmony then skips it) and capture the behaviour elsewhere; don't leave a dead patch applied.
- **Assembly names**: code that looks up assemblies by name needs the 1.7 names: `UnityEngine*` types are in `UnityStubs`, not `UnityEngine.CoreModule`; the engine is `Waylith*`. `mscorlib` is `System.Private.CoreLib`.
- **Stricter type loading**: CoreCLR rejects invalid types at `Assembly.GetTypes()` time that Mono loaded, so code scanning a broken assembly sees fewer types.
- **Optional, not required**: net10.0 offers `System.Threading.Lock` for `lock` objects (`#if v1_7_OR_GREATER private readonly Lock _lock = new(); #else private readonly object _lock = new(); #endif`). Only use it where you're touching that code anyway.

Things that are **not** 1.7-specific don't belong in this port: if the reference port or your work exposes an unrelated bug, report it to the user rather than fixing it as part of this.

### Investigating with the decompiler

Use the `decompiler-mcp` skill. Registered aliases (check with `list_contexts`; if one is missing, load the 1.6 assemblies from `Game Assemblies/1.6/` in `rimworld-utils` and the 1.7 ones from the game root (see CLAUDE.md's "RimWorld game installation"), and ask the user about anything else rather than searching for the DLLs): `rw16`/`rw17` (Assembly-CSharp), `unitystubs17`, `waylith17`, `corelib17` (System.Private.CoreLib), `harmony3rc2` (Lib.Harmony 3.0.0-rc.2 from the NuGet cache). `compare_contexts` with `namespaceFilter` (e.g. `rw16` vs `rw17`, `namespaceFilter: "Verse"`) lists changed types; `compare_symbols` diffs one member. For every game/BCL method the mod patches, confirm it still exists on 1.7 with the same signature and parameter names, and check its callers (`find_callers`) to confirm it's still on the path the mod expects.

## 4. Tests

- Tests whose expectations legitimately differ on 1.7 get `#if v1_7_OR_GREATER ... #else ... #endif` around the whole test or the differing expectation, with a one-line comment saying why (e.g. a type count that drops because CoreCLR rejects a broken fixture type).
- Add a regression test, under `#if v1_7_OR_GREATER`, for each runtime difference you fixed in step 3 (e.g. a re-targeted patch that now runs). These need the real game, so they're DevTools `MainMenu` tests that call the real code path, with `[ErrorsAllowed(...)]` for expected log output.
- Shared test helpers with per-version bodies go in the test project root.

## 5. Verify

- `csharpier format` the changed sources.
- `.vscode/build.sh <version>` for every version in `About.xml` `<supportedVersions>`, now including 1.7. `touch` changed `.cs` files before rebuilding so warnings reappear. Every version: 0 errors. Every version except 1.7: 0 warnings. 1.7: only the warnings step 3 says to leave alone.
- Summarize for the user: project-file changes, each 1.7 code branch and why, tests added, the number of 1.7-only warnings left alone (just the count), patches to confirm in game, and anything left for them (e.g. shared-tooling issues). Leave running the game and the tests, and the commit, to the user.
