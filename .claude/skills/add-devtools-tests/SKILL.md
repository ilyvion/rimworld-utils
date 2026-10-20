---
name: add-devtools-tests
description: "Add a DevTools (SmashPhil) test project to an existing RimWorld mod that has no tests yet: <Mod>.Tests csproj, .sln entry with the Release-build exclusion, InternalsVisibleTo, multi-version build and release-split verification, GABS launch inputs. Sets up the infrastructure only; writes no tests. Use when asked to add testing/tests/a test project to a mod that doesn't have one."
---

# Adding a DevTools test project to an existing mod

Sets up an empty, buildable `<Mod>.Tests` project run in-game by DevTools. Writes no test fixtures and changes no mod behavior. Read `devtools.md` first, at least "Overview", "Referencing and discovery" and "Project setup used by RealisticOrbitalTrade". It sits in the shared directory next to `CLAUDE.md`; find that directory as described at the top of `CLAUDE.md`.

**Reference project:** `../RealisticOrbitalTrade` (`Source/RealisticOrbitalTrade.Tests/RealisticOrbitalTrade.Tests.csproj` and `RealisticOrbitalTrade.sln`).

Ask the user (AskUserQuestion) whenever something is unclear; don't guess. Do not commit unless told to.

## 0. Preconditions

Check these first and stop with an explanation if one fails:

- **No test project exists yet.**
  - A `<Mod>.Tests` project referencing `RimTestRedux.csproj` needs the `rimtest-to-devtools` skill instead.
  - One referencing `DevTools.csproj` means there's nothing to do.
- **The mod uses the shared tooling:** `Directory.Build.props` imports `../rimworld-utils/Common.props`. If it doesn't, the `convert-rimworld-shared` command comes first.
- **DevTools can build for at least one supported version.** DevTools supports only RimWorld 1.6 and 1.7.
  - "The DevTools versions" below means whichever of those are in the mod's `About/About.xml` `<supportedVersions>`.
  - "The other versions" means the remaining supported versions (e.g. 1.4, 1.5).
  - If neither 1.6 nor 1.7 is supported, tell the user that tests can't run until the mod supports one of them (`update-to-17` adds 1.7).
- **DevTools is cloned at `../Third-Party/DevTools`** (`DevTools/DevTools.csproj` inside it). If it's missing, ask the user where it is; never search the filesystem.
- **Find out whether the main project uses ilyvion.Laboratory.** It does if `Directory.Build.props` or the main csproj sets `<UseLaboratory>true</UseLaboratory>`. The test project must match that.

## 1. Test project

Create `Source/<Mod>.Tests/<Mod>.Tests.csproj` by copying RealisticOrbitalTrade's and replacing `RealisticOrbitalTrade` with `<Mod>`. Then adapt it:

- **Mod-specific content:** remove RealisticOrbitalTrade's own `Publicize` items (`TradeShip` fields). Keep `<UseLaboratory>true</UseLaboratory>` only if the main project uses it.
- **Version conditions:** keep the DevTools `ProjectReference` group conditioned on the DevTools versions. If only one of 1.6/1.7 is supported, drop the other from the condition.
- **Groups for the other versions:** keep the `<Compile Remove="**/*.cs" />` group and the `CS0436` `NoWarn` group, conditioned on the other versions. If there are no other versions, delete both groups and their comments.
- **Comments:** make them describe this mod's actual setup (its `.sln` name, the versions it builds tests for), with no RealisticOrbitalTrade-specific or historical narration.
- **ILRepack:** if the main project ILRepacks dependencies, the 1.7 test build needs the main project to set `<ProduceReferenceAssembly>false</ProduceReferenceAssembly>` (see devtools.md "Project setup").
- **Integration projects:** if the mod has them (`Source/<Mod>.<Integration>`), ask whether they should get their own test projects. Those keep their `Integrations/<X>/<version>/Assemblies` output (devtools.md, last bullet of "Project setup").

## 2. Solution

Add the project to `<Mod>.sln` by hand, not with `dotnet sln add`, because that one adds a `Release|Any CPU.Build.0` line.

- Add a `Project(...)` / `EndProject` pair, using the same project type GUID as the main project and a fresh uppercase GUID from `uuidgen`.
- In `ProjectConfigurationPlatforms`, add `Debug|Any CPU.ActiveCfg`, `Debug|Any CPU.Build.0` and `Release|Any CPU.ActiveCfg`, but **no** `Release|Any CPU.Build.0`. That missing line is what keeps the test assembly and DevTools out of Release/CI builds.

## 3. Internals access

Tests reach the mod's code through `[assembly: InternalsVisibleTo("<Mod>.Tests")]` in the main project. If it isn't already there:

- add it at the top of the main `Mod` subclass file (above the `namespace` line), or
- add it to an existing assembly-attributes file, if the project has one.

Do the same for any integration project that gets a test project.

## 4. Build: 0 errors, every supported version

- Build with `.vscode/build.sh <version>` for every version in `<supportedVersions>`, using clean builds: delete `Source/*/obj` first, or use `--no-incremental`.
- On the DevTools versions, `DevTools.dll` and `<Mod>.Tests.dll` must land in `<version>/Assemblies`.
- **The one expected warning** is `CS0436` (`IgnoresAccessChecksToAttribute`) in `<Mod>.Tests` on the DevTools versions. It goes away once the test project has its own `Publicize` items. Leave it, and mention it to the user.
- Any other warning must be fixed. On the other versions the `NoWarn` group must silence `CS0436`, so those builds report `0 Warning(s)`.
- Format only the files you touched (`csharpier format <file>`). Running it on the whole repo can reformat unrelated files.

## 5. Verify the release split

For each DevTools version:

1. Delete `DevTools.dll` and `<Mod>.Tests.dll` from `<version>/Assemblies`.
2. Run `dotnet build --configuration Release --property RimWorldVersion=<version> <Mod>.sln`.
3. Confirm the build succeeds and neither DLL reappears.
4. Rebuild Debug (`.vscode/build.sh <version>`) to restore the dev state. Until `DevTools.dll` is back, the IDE reports `CS0246` on `DevTools.Testing` types.

No CI changes are needed: Release builds skip the test project, and a Release restore works without a DevTools checkout.

## 6. GABS launch inputs

Running the suite through GABS needs the mod's GABS game entry to declare the `runTests`, `testFilter` and `testPlan` launch inputs (devtools.md "Verified workflow").

1. Check with `games_show` for the project's game. Use the GABS MCP tools only, never the `gabs` CLI.
2. If the inputs are missing, propose adding them to that game's entry in `~/.gabs/config.json`, copied from another game that already has them. This is the user's own config, so get their go-ahead before editing.
3. If the mod has no GABS game entry at all, tell the user. Don't create one unasked.

## 7. Finish

Summarize for the user:

- the files created or changed;
- the versions tests build for, and which versions get an empty test assembly;
- the expected `CS0436`;
- the GABS state.

There are no tests yet, so nothing can be run. Suggest `/rimworld-testing-opportunities` to find what to test first. Leave the commit to the user.
