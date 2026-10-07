---
name: rimtest-to-devtools
description: "Convert a RimWorld mod project's unit tests from RimTest Redux to SmashPhil's DevTools: csproj, test sources, load folders, result whitelisting, GABS launch inputs, and verification by an actual test run. Use when asked to migrate/convert a project's tests from RimTest (Redux) to DevTools."
---

# Converting a mod's tests from RimTest Redux to DevTools

Converts existing tests only; do not add or change test logic. Everything you need about DevTools itself is in `devtools.md` (next to this repo's `CLAUDE.md`; resolve the shared directory as described at the top of `CLAUDE.md`). **Read `devtools.md` in full first**, especially "RimTest Redux -> DevTools translation", "Verified workflow", "Whitelisting expected log output" and "Project setup used by RealisticOrbitalTrade". Don't inspect DevTools source unless devtools.md is silent.

**Reference conversion:** commit `664429db0cac28af63f208a8c0ae997128a2bb6d` in the RealisticOrbitalTrade repo (`git -C ../RealisticOrbitalTrade show 664429d`) is the finished result of this procedure: two test projects, ~52 files. Look at its csproj, `LoadFolders.xml` and a couple of test-file diffs to compare against yours. Its `Configs/UnitTestConfig.xml` whitelist is superseded: that repo's commit `5835d74` moved it to per-test `[ErrorsAllowed]`/`[WarningsAllowed]` attributes, which is what step 4 uses.

Ask the user (AskUserQuestion) whenever something is unclear; don't guess. Do not commit unless told to.

## 0. Preconditions

- The project has one or more `<Mod>.Tests` projects referencing `RimTestRedux.csproj` (there can be several, e.g. `<Mod>.<Integration>.Tests`; convert all so no RimTestRedux reference remains).
- DevTools is cloned at `../Third-Party/DevTools` (relative to the project root; remote `https://github.com/ilyvion/SmashPhil-DevTools.git`, branch `develop`). DevTools supports RimWorld 1.6 only and needs the .NET 10 SDK. If the clone is missing, ask the user where it is; never search the filesystem.
- GABS >= 1.1.0 is used through its MCP tools only (never the `gabs` CLI).
- Record the baseline: `grep -rc "\[Test\]"` over the test sources, summed, so you can compare it with the DevTools run's passed count at the end. If practical, run the RimTest Redux suite first (see `gabs.md`) to confirm the tests are green before changing anything.

## 1. Project files

For each test csproj, following the reference commit:

- Drop the `OutputPath` override for the main `<Mod>.Tests` project (Common.props' default `../../$(RimWorldVersion)/Assemblies` puts it next to the mod assembly); an integration test project keeps its `Integrations/<X>/<version>/Assemblies` output so it loads only with that mod.
- Replace the RimTestRedux `ProjectReference` (condition `RimWorldVersion == 1.6`) with the DevTools one from devtools.md's "Project setup" section (`$(ProjectDependenciesBasePath)\Third-Party\DevTools\DevTools\DevTools.csproj` with `AdditionalProperties="OutDir=$(MSBuildThisFileDirectory)..\..\1.6\Assemblies\"`, `CopyLocal=False`, `Private=false`). Keep the `<Compile Remove="**/*.cs" />` for other versions and the `CS0436` `NoWarn` group.
- Rewrite the comments in the csproj so they describe DevTools (no RimTest Redux mentions, no history narration).
- If the mod assembly binds to types from the full `Lib.Harmony` `0Harmony` that its reference assembly lacks (e.g. `IsExternalInit` for `init` properties, typically with `<Publicize Include="0Harmony" />`), the test build fails with `CS7069: Reference to type 'IsExternalInit' claims it is defined in '0Harmony', but it could not be found`: DevTools' transitive `Lib.Harmony.Ref` wins the reference conflict. Add `<PackageReference Include="Lib.Harmony.Ref" Version="2.4.2" ExcludeAssets="all" />` to the test project's 1.6 `ItemGroup`, with a comment saying why (see devtools.md "Project setup").
- Keep the `.sln` as is: the test projects must still lack `Release|Any CPU.Build.0` lines. Don't add any CI/workflow steps: Release builds don't build test projects, and a Release restore works without a DevTools checkout (verify in step 5).
- `LoadFolders.xml`: delete the `IfModActive="ilyvion.rimtestredux"` entry. Nothing replaces it.
- Delete the now-stale gitignored `Integrations/RimTestRedux/` build output folder if present, and remove `ilyvion.rimtestredux` from the local `.savedatafolder/<version>/Config/ModsConfig.xml`. If that leaves `Integrations/` empty (no real integrations), also remove `"Integrations"` from `EXTRA_FILES` in `.vscode/build_config.sh`; check `git log -S'"Integrations"' -- .vscode/build_config.sh` to confirm it was only added for RimTest Redux.

## 2. Test sources

1. Run `python3 <this skill dir>/convert_rimtest_asserts.py Source/<Mod>.Tests [more dirs]`. It rewrites usings, `[TestSuite] static class` to `[TestFixture(TestType.MainMenu)] sealed class`, `[BeforeEach]/[AfterEach]` to `[SetUp]/[TearDown]`, and every `Assert.That/ThatCollection` chain to `Expect.*` with operands swapped as needed. It lists on stderr whatever it could not translate.
2. Convert the listed leftovers by hand per the translation table in devtools.md (`ThatFunc(...).Not.Throw()` becomes a plain call; `ThatFunc(...).Throw()` becomes `Expect.Throws<Exception>(...)`; `BetweenInclusive/Exclusive` become two comparisons; `[ShouldThrow]` becomes `Expect.Throws<E>` in the body).
    - `Does.Throw()`/`Does.Not.Throw()` are the same as `Throw()`/`Not.Throw()`.
    - A lambda that returns a non-statement expression (`() => x.OptionalInt("a") ?? 0`) can't be an `Action`; drop the non-call part (`() => x.OptionalInt("a")`).
    - An expression-bodied test `=> Assert.ThatFunc(() => { ...block... }).Does.Not.Throw();` becomes a block-bodied method containing the lambda's statements, dedented. `return` statements inside local functions in that block are unaffected. Several identical-shape cases in one file can be done with a regex that captures the block and dedents it by one level.
    - `Expect.Throws<E>(...)` used as a statement (not as an expression body) triggers IDE0058; write `_ = Expect.Throws<E>(...)`.
3. Grep the sources for leftover `Assert`, `RimTest`, `TestSuite` in comments and update them so they're accurate (e.g. "RimTest Redux runs before any game is loaded" becomes "these tests run at the main menu, before any game is loaded"). Also check non-test project code for RimTest mentions in comments.
4. Fix nullable warnings the rewrite exposes (`plan.Count` on a nullable `plan` needs `plan!.Count`).
5. `DevTools.Testing` declares types whose names clash with Harmony's (`MethodType`, CS0104 in files using `[HarmonyPatch(..., MethodType.X)]`); add `using MethodType = HarmonyLib.MethodType;` to those files.
6. `csharpier format Source/<Mod>.Tests ...`.

Tests needing a live map or game (rare in RimTest suites, which ran before any game loaded) would need `TestType.Playing`; if a test's comments say it can't run without a map, keep `MainMenu`. Converting to `Playing` is new test logic, outside this skill's scope: ask the user, and if they want it, follow devtools.md's "End-to-end (`Playing`) tests" section (shared state must be restored, ticks driven by the test, real code paths driven).

Two behaviours differ from RimTest Redux and can surface after conversion: any logged warning/error fails a test (step 4), and `Expect.*` doesn't throw, so add `if (x == null) return;` after an `Expect.IsNotNull(x)` or `Expect.IsTrue(x is not null)` (the script's output for `Assert.That(x is not null).Is.True()`) that later code dereferences.

## 3. Build: 0 warnings, 0 errors, every supported version

`.vscode/build.sh <version>` for each version in `About.xml` `<supportedVersions>`. `touch` the test `.cs` files before rebuilding so warnings reappear. Expected fixes:
- CA1852: make fixtures `sealed`.
- CA1829/CA1826: the script turns `Has.Count(n)` into `.Count()`, so these are usually the bulk (hundreds). Replace `.Count()` with the property the warning names (`Count` or `Length`) at each reported line/column, e.g. with a small script that parses the build log; `.Where(p).Count()` flagged IDE0120 becomes `.Count(p)`.
- CS8602: null-forgiving.
- CA2000 on objects created inline in code that used to be inside a `ThatFunc` lambda (the analyzer skips lambdas), e.g. `client.PostAsync(uri, new StringContent(...)).GetAwaiter().GetResult()`. Hoisting it into `using var` trades it for a CA2025 false positive (the analyzer doesn't treat `GetAwaiter().GetResult()` as completing the task). For `StringContent` (an in-memory buffer), keep the inline creation and put `#pragma warning disable CA2000` with a one-line reason after the file's `namespace` line.

Warnings are not acceptable to leave.

## 4. Whitelist and GABS launch inputs

- If the run in step 6 shows tests failing with `Logged warning/error not whitelisted for tests`, confirm the logging is the code under test's intended behaviour, then whitelist it with narrow-regex `[ErrorsAllowed(...)]`/`[WarningsAllowed(...)]` attributes (see devtools.md "Whitelisting"), not the global `warningsAllowed`/`errorsAllowed` lists in `Configs/UnitTestConfig.xml`. Put the attribute on the test method that logs. When every test in a fixture triggers the same log (e.g. each test scans the whole test assembly and hits a deliberately broken fixture class), put it on the fixture class, with a regex naming exactly the expected sources and a comment saying why.
- A failing test's line shows only the **first** unwhitelisted log message, so fixing one can reveal another on the next run. Before rerunning, grep `.savedatafolder/<version>/Player.log` for every distinct message of that kind (e.g. `grep -oE "Skipping [^ ]+ while scanning" Player.log | sort | uniq -c`) and cover them all at once.
- Check `games_show` for the project's GABS game: if it has no `runTests` launch input, tell the user and propose adding all three launch inputs (`runTests`, `testFilter`, `testPlan`; copy them from another converted game in `~/.gabs/config.json`, or from devtools.md "Verified workflow") to that game's entry (it's user config; get their go-ahead before editing). `games_show` reflects the edit immediately.

## 5. Verify the release split

Simulate CI: temporarily delete built `DevTools.dll` and `<Mod>.Tests.*` from `1.6/Assemblies`, run `dotnet build --configuration Release --property RimWorldVersion=1.6 <Mod>.sln` (with a `ProjectDependenciesBasePath` that lacks `Third-Party/DevTools` if you want to prove no checkout is needed; it needs symlinks to `ilyvion.Laboratory` and `rimworld-utils` in that directory), confirm it succeeds and neither DLL reappears (warnings from `ilyvion.Laboratory` in that build come from compiling it through the symlink, outside its own `.editorconfig`, and can be ignored; check that none come from the mod's own sources), then rebuild Debug (`.vscode/build.sh 1.6`) to restore the dev state.

## 6. Run the tests

Follow `gabs.md` "Running the tests with DevTools" (this is a full, unfiltered run, since the passed count must match the baseline; use `testFilter` for any intermediate debugging runs): `games_start` with `launchInputs: {"runTests": true}`, background-wait with `until grep -q "Test runner finished. Result:" ".savedatafolder/1.6/Test.log"`, then count `[Passed]`/`[Failed]` lines. All baseline tests must pass and the passed count must equal the `[Test]` count from step 0. Investigate failures: a failure that is a logged warning/error is a whitelisting question (step 4), anything else is a real conversion mistake (commonly swapped operands: `AreEqual(expected, actual)`).

## 7. Finish

- Final grep: no `RimTest` / `rimtestredux` left in the project (excluding `.git`, `obj`, `.deps`, `.savedatafolder`, notes files the user owns).
- Re-run `csharpier` and the multi-version build if you touched anything after step 3.
- Summarize: files converted, whitelist entries added and why, anything skipped, test counts before/after. Leave the commit to the user.
