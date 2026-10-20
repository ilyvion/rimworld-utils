# DevTools (SmashPhil) reference

Reference for agents converting RimWorld mod unit tests from **RimTest Redux** (`../RimTest`, `[TestSuite]`/`[Test]`, fluent `Assert.That(..)`) to **DevTools** (https://github.com/ilyvion/SmashPhil-DevTools, branch `develop`, C# namespace `DevTools`/`DevTools.Testing`). Everything below was verified against the DevTools source at commit `0e351de` (2026-10-20) unless marked _(unverified)_. Paths in parentheses are relative to the DevTools repo root; the library lives in `DevTools/`.

Contents: [Overview](#overview) · [Referencing and discovery](#referencing-and-discovery) · [Writing tests](#writing-tests) · [Assertions](#assertions-expect) · [RimTest Redux translation](#rimtest-redux--devtools-translation) · [Retrieving test results (agents)](#retrieving-test-results-agents) · [Launching](#launching-tests) · [Gotchas](#gotchas) · [Helper types](#helper-types)

## Overview

DevTools is an SDK, not a standalone mod: the repo has no `About.xml`. Its `DevTools.dll` (`AssemblyName` `DevTools`, version 1.0.0) is dropped into a host mod's `Assemblies` folder and initialises itself from a `[StaticConstructorOnStartup]` class (`DevTools/DevHarmony.cs`). `DevTools.csproj` builds for the `RimWorldVersion` property (default `1.7`, set in the repo's `Directory.Build.props`):
- `1.6`: net48 against `Krafs.Rimworld.Ref 1.6.*` and `Lib.Harmony.Ref 2.4.3`, defining `v1_6`. The test manifest (`DataWriter.cs`, `IJsonSerializable.cs`, needs `System.Text.Json`), the TCP result stream (`Testing/Test/TcpBridge.cs`, `Testing/TestMessage.cs`, needs `System.Text.Json` and `init` accessors) and `ScopedRollback<T>` (needs ref fields) are left out; `SaveMetaData` is a no-op, `ConnectTcp` doesn't exist, and `--port`/`--session` only log a warning. The child-process path comes from `Process.GetCurrentProcess().MainModule.FileName` instead of `Environment.ProcessPath`.
- `1.7`: net10.0 against `Krafs.Rimworld.Ref 1.7.*-*` and the newest `Lib.Harmony 3.0.x` (prereleases included in both), defining `v1_7`.

Harmony is referenced with `PrivateAssets="all"`, so it doesn't flow into projects referencing DevTools. Each version restores into its own `obj\<version>\` and builds into `..\..\<version>\Assemblies\`.

Subsystems (each is an `IDevTool` registered per running mod, and only if that mod's assemblies contain a matching type):

| Subsystem | Marker | Purpose |
|---|---|---|
| Unit tests (`TestFixtureManager`) | `[TestFixture(TestType)]` classes with `[Test]` methods | In-game unit tests with scene management (main menu / generated world+map / post-game main menu), setup/teardown, parameterised tests, coroutine tests, retries, filters, log-failure detection, Test Explorer UI |
| Smoke tests (`SmokeTestManager`) | internal `[SmokeTest]` | Built-in startup-log check; `--smoke-test` |
| Test plans (`TestPlanManager`) | `<mod>/TestPlans/*.xml` | Spawn child game processes with specific mod lists and CLI args; parent waits for exit code |
| Benchmarks (`BenchmarkManager`) | `[BenchmarkClass]`, `[Benchmark]`, `[Prepare]`, `[OnFinish]`, `[Context]`, `[Measurement]`, `[Table]`, `[Graph]` | Micro-benchmark harness on static methods, results in a table dialog or `Log.Message` |
| UI | `Dialog_TestExplorer`, `Dialog_TestSuspension`, `DataTable`, `BarChart`/`Graph` | Test Explorer window (tree, status icons, duration, error message, summary panel, "Open Log") |
| Dev-mode entry | Harmony postfix on `DebugWindowsOpener.DrawButtons` | Adds a "DevTools" icon button to the dev-mode debug-windows row |

`DevTools.TestAdapter/` is a separate net10.0 project: a VSTest adapter for `dotnet test` and IDE test explorers _(unverified in a live run)_. Discovery launches the game with `--test-discovery=<packageId>` and reads the JSON manifest it writes; execution launches it with `--pid <id> --test --test-list <file> --exit --port <port> --session <id> -NoWindow` and receives results over TCP. It reads the `TestRunParameters` `DevTools.PackageId`, `DevTools.RimWorldExePath` and `DevTools.AutoRecacheOnBuild` from the run settings.

The `feature/quickactions` (startup actions) and `microbenchmarks` / `parametric-tests` branches are unmerged; `develop` is the default branch and the only one described here.

## Referencing and discovery

- **No mod dependency / packageId** is needed for tests themselves: DevTools scans every running mod (`LoadedModManager.RunningModsListForReading`), and for each `mod.assemblies.loadedAssemblies` calls `GetTypes()` looking for `[TestFixture]` (`DevHarmony.LoadTypes`). The test assembly must therefore be an assembly loaded by a mod content pack (i.e. sit in a folder listed in `LoadFolders.xml` / the mod's `Assemblies`). DevTools registers the tool for a mod only if it found at least one fixture in that mod's assemblies.
- **DevTools.dll must itself be loaded** by some active mod: RimWorld loads it as a plain assembly from a mod's `Assemblies` folder. `DevTools.csproj` sets `OutDir` to `..\..\<version>\Assemblies\` (two directories above the csproj directory), i.e. it is meant to be cloned as a directory at the mod root (`<Mod>/DevTools/DevTools/DevTools.csproj`, output `<Mod>/<version>/Assemblies/DevTools.dll`). When consuming it as a `ProjectReference` from another repo, override `OutDir`/`OutputPath` (global property) or copy the built DLL to where the game loads it.
- Test project references: `DevTools` (project or DLL, `Private=false` unless it is the copy that ships to the game). Test code needs `using DevTools.Testing;` (and `using DevTools;` for `MapUtils`, `RandBlockPersistent`, `StackTraceCacheDisabler`). Verse/RimWorld/UnityEngine come from `rimworld-utils/globalusings.cs`.
- **Build requirements**: `DevTools.csproj` uses `LangVersion=preview`, `TreatWarningsAsErrors=true`, and C# 14 features (`field` keyword, `extension(...)` blocks in `DevUtils.cs` and `Testing/Ext_TestManagers.cs`), so it needs the .NET 10 SDK (10.0.111 is installed locally alongside 9.0.120). Configurations: `Debug`/`ReleaseTesting` define `UNITY_ASSERTIONS`, `Release` does not. Restore uses NuGet (`Krafs.Rimworld.Ref`, `Lib.Harmony.Ref`, `Lib.Harmony`); `packages.config` is legacy.
- **Fetching (CI/local)**: there is no README instruction, submodule, NuGet package, release artifact, or GitHub Action in the repo (README is 2 lines: "Developer tools for rimworld mods"). The remote is `https://github.com/ilyvion/SmashPhil-DevTools.git`, default branch `develop` (`origin/HEAD -> origin/develop`), no tags. The local clone is `/home/alex/Documents/Projects/RimWorld/Third-Party/DevTools`. The sibling-repo pattern used for RimTest (`$(ProjectDependenciesBasePath)\RimTest\...` via `rimworld-utils/Common.props`, `ProjectDependenciesBasePath` default `..\..\..`) would require CI to `actions/checkout` `SmashPhil/DevTools` at a pinned SHA into that location; `rimworld-utils/.github/workflows/build_mod.yml` only checks out the mod, `ilyvion/rimworld-utils` and (when a project built in the solution configuration uses `UseLaboratory`) `ilyvion/ilyvion-laboratory` (no RimTest checkout), and test projects are not built in Release. CI therefore needs no DevTools step today (a Release restore of the `.sln` works without a checkout). If tests are ever run in CI: add an `actions/checkout` of `SmashPhil/DevTools` (`develop`) to the mod's `post_checkout_hook`, moved to `../Third-Party/DevTools`, plus a .NET 10 SDK.
- **LoadFolders / About.xml**: no `IfModActive` gate is possible because DevTools has no packageId. Put `DevTools.dll` and the test assembly in the mod's always-loaded `<version>/Assemblies` (gitignored) and keep them out of releases by leaving the test project out of the Release build (see "Project setup used by RealisticOrbitalTrade"; verified in a live run).
- **Discovery timing**: at startup from `[StaticConstructorOnStartup]` (defs are loaded). `TestFixtureManager.Init` sorts by execution priority, loads `Configs/UnitTestConfig.xml`, and does a dry-run discovery (`Test.Discover`). The JSON test manifest is written only for `--test-discovery` (see "Launching tests"). If two mods each ship a `DevTools.dll`, behaviour is unknown _(unverified)_.
- Exceptions from `assembly.GetTypes()` (e.g. `ReflectionTypeLoadException`) are not inside the per-type `try` in `DevHarmony.TryLoadDevTool`; a mod assembly with unloadable types would break discovery for that tool.

## Writing tests

### Fixtures (test classes)

```csharp
using DevTools.Testing;

namespace MyMod.Tests;

[TestFixture(TestType.MainMenu)]
internal class WithCountTests           // NOT static
{
    [Test]
    public void CopiesAllFields()       // instance or static both work
    {
        Expect.AreEqual(4, copy.count);
    }
}
```

- `[TestFixture(TestType)]` on a class (`Testing/Test/Attributes/TestFixtureAttribute.cs`). **Static classes are silently skipped** (`type.IsAbstract` is true for static classes, `TryRegisterType` returns false without logging). A fixture must be non-abstract and have **exactly one public instance constructor**, otherwise `Log.Error("Unable to register X. It must have 1 public constructor.")` (`TestFixtureManager.TryRegisterType`). An implicit default constructor on an `internal` class satisfies this.
- A **new instance is created per fixture run** (`Activator.CreateInstance`), shared across all `[Test]` methods of that fixture (fields persist between tests in the same run; not re-created per test). Setup/teardown and tests receive that instance; static test methods are invoked with it ignored.
- Fixture name = `Type.Name` (no namespace) in logs and UI; registry key is `Namespace.Name` (+ ctor args); a duplicate key is dropped silently (`TryAdd`).
- `TestType` values: `MainMenu` (default program state, no game loaded), `Playing` (fresh world + map generated first, `Find.CurrentMap` valid), `PostGameExit` (game generated, then exits to main menu, tests run there). Fixtures run ordered by `TestType` (MainMenu, Playing, PostGameExit), then `[ExecutionPriority]` (higher first, `Priority.First = int.MaxValue`).
- Scene changes happen only when the type changes (or the fixture has a save/custom game settings). Consecutive `Playing` fixtures share one map.

### Attributes

| Attribute | Target | Meaning |
|---|---|---|
| `[TestFixture(TestType)]` | class | Marks a fixture |
| `[Test]` | method | A test. `void` = sync; `IEnumerator` = coroutine test (see below) |
| `[TestCase(args..., ExpectedResult = x)]` (`AllowMultiple`; inherits `[Test]`) | method | Inline args; compares return value to `ExpectedResult` (floats via `Assert.AreApproximatelyEqual`, else `Assert.AreEqual`) |
| `[SetUp]` / `[TearDown]` | method (`void` only) | Before/after each test. Multiple allowed, ordered by `[ExecutionPriority]`. TearDown runs in a `finally` |
| `[OneTimeSetUp]` / `[OneTimeTearDown]` | method | Once per fixture run |
| `[Parameters(a, b, ...)]` | test-method or **fixture-constructor** parameter | Inline values, one test/fixture per value. Only **one** parameter allowed per method/ctor |
| `[ParametersSource("name")]` / `[ParametersSource(typeof(T), "name")]` | parameter | Static field (array) or static method returning `IEnumerable` |
| `[DefsSource(params string[] packageIds)]` / `[DefsSource(typeof(T), "filterMethod", packageIds...)]` | parameter of `Def` type | All defs of that type (optionally from given mods, filter is a static `bool F(Def)`). `[DefParameters]` is `[Obsolete(error: true)]`, so using it fails the build |
| `[Retry(n)]` | method | Retry a failed test up to `n` more times (also global `retryOnFailure` -> 1) |
| `[Disabled]` | class/method | Excluded entirely |
| `[ExecutionPriority(int or Priority)]` | class/method | Ordering (higher first) |
| `[TestCategory("a", "b")]` | class/method | Filterable metadata (`category`) |
| `[WarningsAllowed(params string[] regexes)]` / `[ErrorsAllowed(params string[] regexes)]` | class/method | Logged warnings/errors matching a regex do not fail this fixture/test only |
| `[TestDescription("...")]` | class/method | Tooltip in Test Explorer |
| `[LoadSave("file")]` | class | Load this save instead of generating a game |
| `[LoadIfModsActive(ids...)]`, `[LoadIfAnyModsActive(ids...)]` | class/method | Skip registration unless mods are active (`ModLister.AllModsActiveNoSuffix`) |
| `[LoadIfRoyaltyActive]`, `[LoadIfIdeologyActive]`, `[LoadIfBiotechActive]`, `[LoadIfAnomalyActive]`, `[LoadIfOdysseyActive]` | class/method | DLC gates |

There is no `[Ignore]`, `[Explicit]`, `[Timeout]`, `[ShouldThrow]`, or `[Values]`. `HideInUI` is internal.

Fixture-level game settings are declared by implementing interfaces on the fixture class (`Testing/Test/Interfaces`): `IWorldGeneration { WorldGenerationSettings WorldGenerationSettings {get;} }`, `IMapGeneration { MapGenerationSettings MapGenerationSettings {get;} }`, `IScenario { Scenario Scenario {get;} }`, `IStoryteller { Storyteller Storyteller {get;} }`. Any of them makes the fixture regenerate the game/map; the environment is restored to default for the next fixture. `MapGenerationSettings` fields: `size` (150), `biome`, `mapGeneratorDef`, `extraGenStepDefs`, `postGenerationActions` (`List<Action>`), `Faction`. The default map is an all-concrete empty map with roofs and non-pawn things cleared (`MapGenerationSettings.Default`, `GenStep_EmptyMap`). `WorldGenerationSettings` fields: `percent` 0.05, `startingSeason` Summer, `startingPawnsRequired`, etc.

### Test lifecycle (`TestRunner.TestRoutine`)

1. `LongEventHandler.ExecuteWhenFinished` then a Unity coroutine (`CoroutineObject`) on the main thread; `Application.runInBackground = true` for the duration; one frame skipped.
2. Seed: `Rand` is fixed (`randSeed` from config or random; `Starting tests with seed: N` is logged).
3. Per fixture: create instance -> scene change if needed -> `TestType.Playing`: camera jump to map -> pre-test actions (config) -> `[OneTimeSetUp]` -> for each test: (`[SetUp]` -> test -> `[TearDown]`) with retries -> `[OneTimeTearDown]` -> post-test actions -> log fixture results.
4. Each fixture and each test is wrapped in a `LogWatcher`: **any `Log.Warning`/`Log.Error`/exception logged during it fails the test** unless whitelisted (`[WarningsAllowed]`/`[ErrorsAllowed]` attributes, or the config's `failOnWarnings`/`failOnErrors`, default both `true`, and global `warningsAllowed`/`errorsAllowed` regex lists).
5. At the end the game returns to the main menu, then `OnTestRunnerEnd`: optional report, and either `Application.Quit` (if `--exit`) or opens the Test Explorer.

### Coroutine (multi-tick) tests

A `[Test]` method returning `IEnumerator` is driven by the runner coroutine (`TestFunction.ExecuteRoutine`); exceptions inside are caught and fail the test. Yield `null` (next frame) or any Unity yield instruction:

```csharp
[Test]
public IEnumerator PawnFinishesJob()
{
    Pawn pawn = ...;
    yield return new WaitTicks(60);                                  // game ticks
    yield return new WaitUntilTimeout(() => pawn.Dead, maxTicksToWait: 600); // Test.Fail("Timed out") on timeout
    yield return pawn.WaitJob(JobDefOf.Wait, timeoutTicks: 2500);            // WaitJobType.StartsJob | StartsAndFinishesJob | DoesntStartJob
    yield return Test.Suspend(secondsTimeOut: 30, "Inspect the map");        // dialog; -1 = wait for close
}
```
`WaitTicks`, `WaitUntilTimeout`, `WaitJob` count `Find.TickManager.TicksGame`; they only progress if the game is actually ticking (nothing in DevTools unpauses or sets speed) _(unverified in a live run)_. Async/`Task` and threads are not supported: tests run on the main thread.

### Creating maps, pawns, things

There are no factories for pawns/things. Use vanilla APIs (`ThingMaker.MakeThing`, `PawnGenerator.GeneratePawn`, `GenSpawn.Spawn`) on `Find.CurrentMap` in a `TestType.Playing` fixture, and clean up with `ScopeEntity`/`ScopeWorldObject`/`ScopeWindow` or `MapUtils.KillEverything(map)`. Pure-logic tests that only use `new Pawn()` / `new Thing()` (as the RimTest Redux tests do) work in `TestType.MainMenu`.

### End-to-end (`Playing`) tests: the scene harness pattern

Reference: RealisticOrbitalTrade's `Source/RealisticOrbitalTrade.Tests/TradeScene.cs` and the `*EndToEndTests.cs` fixtures using it. For any test that needs a live colony, build one shared, disposable **scene class** instead of putting setup in each fixture:

- **`internal sealed class XScene : IDisposable`.** The constructor builds everything the tests need (home area, powered buildings, colonists, goods, the trader/quest giver); helper methods do the player-level actions (`SellAndWaitForCompletion`, `CancelTradeFromShuttle`, ...); shared assertions live there too (`ExpectSaleCompleted`) so a test only adds its own. Tests are `using var scene = new XScene(Find.CurrentMap);` followed by placing things, acting, and asserting. Extend the scene rather than copying setup into fixtures.
- **Tests share one world and map.** DevTools only regenerates them for a fixture with custom generation settings, so `Dispose` must put back everything a test touched: mod settings, temporarily changed defs (e.g. a comp's capacity), time speed, home area cells added, quests, letters, alerts, transport ships, spawned things and pawns, open windows, static "current X" fields. Anything a test newly changes on shared state goes into `Dispose`.
    - Snapshot what existed before (`HashSet` of ships/letters) and remove only what is new.
    - Destroying a pawn or shuttle drops its contents onto the map; sweep leftover items in a few passes, then `Expect` the map is clean.
    - Have the constructor `Expect` that the previous test left nothing behind (leaked quests, items, ...), so a leak is reported at its source instead of as a mystery in a later test.
    - Only end quests that are `QuestState.Ongoing`: ending a finished one logs `Tried to resolve a historical quest`, which fails the test. Removing a quest from the `QuestManager` before its alerts are dropped leaves the alert on screen for good.
- **Drive the real player path.** Open comms/dialogs the way the UI does (`TryOpenComms`, `TradeSession.deal`, `Tradeable.ForceTo`, `deal.TryExecute`, invoking a button's stored action) so the patches under test run. Writing fields directly skips them.
- **Set every setting explicitly.** Use a `readonly record struct SettingsSnapshot` with `Defaults`, `Capture()` and `Apply()`; the scene applies it (defaults unless the test passes `Defaults with { X = ... }`) and restores the captured values in `Dispose`, so results never depend on the player's saved config.
- **Control time yourself.** Set `Find.TickManager.CurTimeSpeed = TimeSpeed.Paused` and advance with `Find.TickManager.DoSingleTick()` in an `IEnumerator` test: tick a batch (~60), `yield return null`, and loop on a condition with a tick timeout rather than a fixed tick count. This makes `WaitTicks`/`WaitUntilTimeout` unnecessary and independent of game speed (verified in live runs). Restore the speed in `Dispose`.
- **Colonists must be capable.** Random colonists can have a needed stat/work type disabled (a disabled `TradePriceImprovement` logs an error and fails the test). Regenerate until `WorkTypeIsDisabled`/`IsDisabledFor` are false for what the test needs, and set the timetable to `Anything` for all 24 hours so they work regardless of the hour. Set `workSettings.SetPriority(def, 0)` to stop a pawn doing a job.
- **Prefer shrinking the world over hauling more.** To make a task big, lower a capacity (e.g. shuttle `massCapacity`, restored in `Dispose`) instead of using a heavy item that needs thousands of trips.
- **Assert on things, not cached counters.** Vanilla counters are often incomplete (`Map.resourceCounter` counts only stored items; a trader's `CountHeldOf` can miss stacks added by quest parts). Sum `Map.listerThings.ThingsOfDef(def)` or the trader's goods by stack count instead.
- **Record what a scenario started with** (a small `Sale`-like class: counts before, amount agreed, quest, ships) so the shared assertion can compare before/after, and update it when a test changes the deal midway.
- **Interference points.** Expose helpers that stop at an intermediate state (`...AndWaitForLanding`, `WaitForPartialLoad`) so a test can cancel, renegotiate or expire the scenario mid-flight.
- **Tests that need another mod:** `[LoadIfModsActive(...)]` plus a test plan in `TestPlans/` that loads that mod; run it with the `testPlan` launch input.

## Assertions (`Expect`)

`DevTools.Testing.Expect` (`Testing/Expect.cs`) is the assertion class. It is **non-throwing and non-fluent**: each call records status on the current test and execution continues; the first failure's context/message is kept (`??=`). `Expect` outside a running test logs `Log.Error("Using Expect outside of test watcher...")`. Every method takes an optional trailing `string message = null` (shown as the fail label). `UnityEngine.Assertions.Assert` also works but throws (and only when `UNITY_ASSERTIONS` is defined in the calling assembly, otherwise the calls are compiled out); exceptions are caught by `Test.Fail(ex)`. Prefer `Expect`.

Full surface:

| Method | Passes when |
|---|---|
| `That(Func<bool> validator, msg)` | validator() true |
| `IsTrue(bool, msg)` / `IsFalse(bool, msg)` | |
| `IsNull<T>(T, msg)` / `IsNotNull<T>(T, msg)` (`T : class`) | `obj == null` / `!= null` |
| `AreEqual<T>(T expected, T actual, msg)` / `AreNotEqual` | `EqualityComparer<T>.Default` |
| `AreApproximatelyEqual(float expected, float actual, msg)` / `AreNotApproximatelyEqual` | Unity `FloatComparer` default tolerance |
| `ReferencesAreEqual<T>(expected, actual)` / `ReferencesAreNotEqual` (`T : class`) | `ReferenceEquals` |
| `GreaterThan<T>(value, operand)`, `GreaterThanOrEqualTo`, `LessThan`, `LessThanOrEqualTo` (`T : IComparable`) | `value <op> operand` |
| `IsEmpty<T>(IEnumerable<T>)` / `IsNotEmpty` | no elements / any element |
| `All<T>(items, Func<T,bool>)`, `Any`, `None` | predicate quantifiers |
| `T Throws<T>(Action, msg)` (`T : Exception`) | action throws `T` (or subtype); returns the exception |

Other test-control methods on `Test`: `Test.Fail(string message, string failureMessage = null)`, `Test.Fail(Exception)`, `Test.Skip(msg)`, `Test.Cancel(msg)` (all only set status; **the test method keeps executing**, `return` yourself), `Test.Current` (`ITestGroup`), `Test.Suspend(...)`.

Gaps: no collection `Count`/`Contains`/element-equality (`SequenceEqual`), no string assertions, no `IsInstanceOf`, no `Between`, no fluent/negation grammar, no message-lambda overloads. Use `Expect.AreEqual(n, list.Count)`, `Expect.IsTrue(list.Contains(x))`, `Expect.That(() => a.SequenceEqual(b))`.

## RimTest Redux -> DevTools translation

Argument order matters: `AreEqual(expected, actual)`.

### Structure

| RimTest Redux | DevTools |
|---|---|
| `using RimTestRedux;` | `using DevTools.Testing;` |
| `[TestSuite] internal static class X` | `[TestFixture(TestType.MainMenu)] internal class X` (**remove `static`**; default ctor is fine) |
| `[Test] public static void T()` | `[Test] public static void T()` works as is, or make it an instance method |
| `[BeforeEach]` (max one, static) | `[SetUp]` (may stay static) |
| `[AfterEach]` (max one, static, always runs) | `[TearDown]` (may stay static; runs in `finally`) |
| (none) | `[OneTimeSetUp]`, `[OneTimeTearDown]`, `[TestCase]`, `[Parameters]`, `[Retry]`, `[TestCategory]`... |
| `[ShouldThrow]` / `[ShouldThrow(typeof(E))]` | no attribute; body: `Expect.Throws<E>(() => ...)` (use `Exception` for any) |
| tests needing a live map | `[TestFixture(TestType.Playing)]` |
| log errors are captured and do not fail | logged warnings/errors **fail the test** (see Gotchas) |

### Assertions

| RimTest Redux | DevTools |
|---|---|
| `Assert.That(x).Is.True()` | `Expect.IsTrue(x)` |
| `Assert.That(x).Is.False()` | `Expect.IsFalse(x)` |
| `Assert.That(x).Is.EqualTo(y)` | `Expect.AreEqual(y, x)` (uses `Equals`, RimTest used `CompareTo == 0`; cast enums/ints as before) |
| `Assert.That(x).Not.EqualTo(y)` | `Expect.AreNotEqual(y, x)` |
| `Assert.That(x).Is.GreaterThan(y)` | `Expect.GreaterThan(x, y)` |
| `Assert.That(x).Not.GreaterThan(y)` | `Expect.LessThanOrEqualTo(x, y)` |
| `Assert.That(x).Is.LessThan(y)` / `Not.LessThan(y)` | `Expect.LessThan(x, y)` / `Expect.GreaterThanOrEqualTo(x, y)` |
| `Assert.That(x).Is.BetweenInclusive(a, b)` | `Expect.GreaterThanOrEqualTo(x, a); Expect.LessThanOrEqualTo(x, b);` (no equivalent) |
| `BetweenExclusive(a, b)` | `Expect.GreaterThan(x, a); Expect.LessThan(x, b);` |
| `Assert.That(x).Is.Null()` / `Not.Null()` | `Expect.IsNull(x)` / `Expect.IsNotNull(x)` (`T : class`; nullable value types need `Expect.IsTrue(x == null)`) |
| `SameValueAs(y)` | `Expect.AreEqual(y, x)` |
| `SameReferenceAs(y)` | `Expect.ReferencesAreEqual(y, x)` |
| `Assert.ThatCollection(c).Has.Count(n)` | `Expect.AreEqual(n, c.Count())` (`System.Linq`) |
| `Assert.ThatCollection(c).Is.Empty()` / `Not.Empty()` | `Expect.IsEmpty(c)` / `Expect.IsNotEmpty(c)` (generic `IEnumerable<T>`; non-generic collections need `.Cast<object>()`) |
| `Assert.ThatCollection(c).Does.Contain(item)` | `Expect.IsTrue(c.Contains(item))` (or `Expect.Any(c, e => Equals(e, item))`) |
| `Assert.ThatCollection(c).Does.Not.Contain(item)` | `Expect.IsFalse(c.Contains(item))` or `Expect.None(c, e => Equals(e, item))` |
| `Assert.ThatFunc(() => ...).Throw()` / `Assert.ThatFunc(Action).Throw()` (also `.Does.Throw()`) | `Expect.Throws<Exception>(() => ...)` (the lambda must be a statement expression; as a standalone statement write `_ = Expect.Throws<...>(...)` for IDE0058) |
| `Assert.ThatFunc(...).Not.Throw()` (also `.Does.Not.Throw()`) | call the code directly (an exception fails the test); a block lambda's statements become the test method's body |
| `Assert.That(null-able).Is.EqualTo` on a null value | RimTest fails with "non-null required"; `Expect.AreEqual` compares nulls normally |

Features the RealisticOrbitalTrade tests use: `[TestSuite]` static class, `[Test]` static void, `[BeforeEach]`/`[AfterEach]`, `Assert.That(..).Is.True()/False()/EqualTo()/Not.GreaterThan()`, `Assert.ThatCollection(..).Has.Count()/Is.Empty()/Does.Contain()`, ad-hoc `new Pawn()`/`new Thing()`/`new ThingDef`, `MemoryStream` scribe round-trips. All are covered by the table. Because tests are `internal static`, the enclosing project's `InternalsVisibleTo` still applies unchanged.

Wholesale-conversion mechanics: make each `[TestSuite] static class` a non-static `[TestFixture(TestType.MainMenu)]` class, rename `[BeforeEach]`/`[AfterEach]`, rewrite each `Assert.*` per the tables, and keep the `[Test]` methods static or not. A mechanical rewrite of `Assert.That(a).Is.EqualTo(b)` to `Expect.AreEqual(b, a)` swaps operands.

## Retrieving test results (agents)

**DevTools does not write results to `Player.log`.** There is no `TESTING START`/`END` marker and no end-of-run summary line. Results go to a separate file written by `DevLog`.

### Where results are written

- File: `Test.log` (`Logger.Config.LogFileName`) in `Logger.Config.folder`, default `Application.persistentDataPath` (Linux/Unity: `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/`; under Flatpak Steam: `~/.var/app/com.valvesoftware.Steam/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/`) _(path not confirmed in a live run; `-savedatafolder` does not change Unity's persistent data path)_. Override the folder via `Configs/UnitTestConfig.xml` in the mod root (`Ext_TestManagers.LoadConfig` reads `<mod.RootDir>/Configs/UnitTestConfig.xml`):

```xml
<TestConfig>
  <log>
    <folder>/absolute/path/writable/by/the/game</folder>   <!-- alias: filePath -->
    <verboseLogging>true</verboseLogging>                  <!-- default true: passing tests are logged too -->
    <maxFileSize>10485760</maxFileSize>                    <!-- bytes; further messages are dropped -->
    <writer>Full.Type.Name</writer>                        <!-- ILogWriter, default TextLogWriter -->
    <report>DevTools.Testing.MarkdownReport</report>       <!-- ITestReport, default none -->
  </log>
  <failOnWarnings>true</failOnWarnings>
  <failOnErrors>true</failOnErrors>
  <warningsAllowed><li>regex</li></warningsAllowed>
  <errorsAllowed><li>regex</li></errorsAllowed>
  <randSeed>12345</randSeed>
  <stopOnFailure>false</stopOnFailure>
  <retryOnFailure>false</retryOnFailure>
</TestConfig>
```
(Field names are the public fields of `TestConfig`/`Logger.Config`; the file is optional, defaults apply when absent. XML shape and `Type`-valued fields are expected to work through `DirectXmlLoader` but were not exercised _(unverified)_.)

- The first logger instance (mutex owner) truncates `Test.log` at the start of each run (`FileMode.Create`); a second concurrent game instance appends.
- Messages are dropped once the file exceeds `maxFileSize` (10 MB) and **`Logger.Write` also drops every message if the log file does not exist** (`Logging/Logger.cs`).
- Flushed after each fixture (`DevLog.Flush()`) and on logger disposal at the end of the run. If the game crashes or the coroutine dies mid-run, completed fixtures are in the file (as flushed) but nothing marks the run as incomplete.

### Exact output format (`Test.LogResults`, `Test.StatusMessage`)

Header written when the log opens (`TextLogWriter.PostInit`, only if the file already exists at that point):
```
{DateTime.Now:"g" current culture}
<blank line>
Starting tests with seed: {seed}
<blank line>
```
Per fixture (written after the fixture finishes; blank line after each fixture):
```
[Passed]    {FixtureName}
--	[Passed]    {TestName}
--	[Passed]    {TestName(arg1, arg2)}
<blank>
```
Line = `{Status label}{padding} {Label}`; padding: `[Failed]` + 4 spaces total before the label, `[Passed]` 4, `[NotRun]` 4, `[Canceled]` 2, `[Skipped]` 3, `[Pending]` 3 (the label constants are `[Failed]`, `[Canceled]`, `[Skipped]`, `[Passed]`, `[Pending]`, `[NotRun]`). Function lines are prefixed by `--` + a TAB. Passing lines are written only when `verboseLogging` is true (default). **Failed lines are always written**, followed by a stack trace.

Failed line shape (fixture line and each failing test line):
```
[Failed]    {Label}  {TestContext}  {FailLabel}  {FailMessage}
{Exception.ToString()}                      <- only when an exception was thrown
{StackTrace}                                <- Exception.StackTrace, else the assertion-site StackTrace
```
- `TestContext` is the assertion name (`AreEqual`, `IsTrue`, `Throws`, ...), `Test.Fail`'s context (`Test.Fail`, `Exception thrown!`, ...); `FailLabel` is your `message` argument; `FailMessage` from `MessageBuilder`, e.g.
  - `AreEqual(5, 4)`: `Assertion failure. Values are not equal.` + newline + `Expected: Expected 5 == 4`
  - `IsTrue(false)`: `Assertion failure. Value was False` + newline + `Expected: True`
  - `GreaterThan(1, 2)`: `Assertion failure. Value was not > 2` + newline + `Expected: 1 > 2`
  - `IsNull(x)`: `Assertion failure. Value was not Null` + newline + `Expected: Value was Null`
  - Logged warning/error: `TestContext` `Logged error not whitelisted for tests.` (or `warning`), `FailMessage` = `"{log text}"` + newline + Unity stack trace.
- Failure info propagates upward: the fixture's line shows the last failure recorded in its children; a failing test therefore appears twice (fixture line, test line).
- Skips/cancels via `Test.Skip`/`Test.Cancel` are logged only at verbose level (`[Skipped]`, `[Canceled]`); they are not failures.
- With `--exit` (see below) the last line written is `Test runner finished. Result: Passed` or `Test runner finished. Result: Failed` (`TestFixtureManager.OnTestRunnerEnd`/`SmokeTestManager.OnTestRunnerEnd`; `Failed` = any fixture status `Failed`). `TestPlanManager.OnTestRunnerEnd` writes a distinct `Test plan finished. Result: Passed|Failed` line instead, for the plan's own overall result — see the `--test-plan` Gotchas bullet for why a plan needs its own marker.

No per-run totals are logged. Counts must be derived by grepping lines, or by a markdown report (below).

### Machine-friendly mechanisms that exist

| Mechanism | Detail |
|---|---|
| **Process exit code** | With `--exit` (or `-NoWindow`), `Application.Quit(1)` if any fixture failed else `Application.Quit(0)` (`TestFixtureManager.cs`, `SmokeTestManager.cs`, `TestPlanManager.cs`). Only reached if the runner completes; a crash or an exception escaping the runner coroutine (e.g. a throwing fixture constructor, exceptions during scene change outside handled paths) leaves the game running with no end line. |
| **Auto-run + auto-quit CLI** | `--pid <packageId> --test [--where "<expr>"] --exit` (details below). |
| **Custom log output** | `ILogWriter` (`PostInit`, `Flush`, `WriteLine(string)`, `Dispose`; public ctor taking `Logger`) chosen via `<log><writer>`; see custom output below. |
| **Custom report** | `ITestReport.Tabulate(string directoryPath, List<ITestGroup> modules)` via `<log><report>`; built-in `DevTools.Testing.MarkdownReport` writes `<folder>/test-report.md` (UTF-8 with BOM, overwritten each run, only for the unit-test manager, at run end before exit). |
| **In-process API** | `TestRunner.Active` / `TestRunner.Current` (public static), `event Action<bool> TestRunner.OnTestRunnerStateChange` (true at start, false at end), `Test.GetGroups(ITestManager)` (tree of `ITestGroup` with `Status`, `FailMessage`, `Exception`, `StackTrace`, `Duration`). Obtaining the manager instance requires `DevHarmony.GetDevTool<TestFixtureManager>(mod)` (internal class; needs Krafs.Publicizer or a bridge that can evaluate reflection). There is no built-in GABP/RimBridge endpoint. |
| **TCP result stream** | 1.7 only. With `--port <port> --session <id>`, `TcpBridge` connects to `127.0.0.1:<port>` and writes one JSON `TestMessage` per line: a `TestResult` (`MessageType` 1) per finished test with `SessionId`, `FullName`, `Status`, `Duration` (ms), `Message`, `StackTrace`, then a `SessionComplete` (2) with the total `Duration`. Used by `DevTools.TestAdapter`. |
| Files other than `Test.log` and `test-report.md` | Only the test manifest `LoadedTestCases-<Assembly>.json` (`DataWriter.cs`, under `Environment.SpecialFolder.ApplicationData` + `Ludeon Studios\RimWorld by Ludeon Studios\DevTools`; the backslashes are literal on Linux), written by `--test-discovery`. No XML/JUnit output. |

`MarkdownReport` output includes the grep-able summary sentence
`{completed} tests were completed in {time} with {passed} passed, {failed} failed, and {skipped} skipped.` (durations `1.23s` or `12.34ms`) and a shields.io badge line `https://img.shields.io/badge/Tests-{passed}%20passed[%2c%20{failed}%20failed]-{success|red}`, plus per-fixture tables and `<pre>` failure blocks with HTML-encoded messages. Counts exclude `NotRun`.

### Custom output formatting: CONFIRMED

The user's recollection is correct. Commits `f58913b` ("Add writer and report interfaces for custom logging and reporting implementations") and `7e73e91` ("Add ILogWriter and ITestReport for configurable logging and test report generation") add `ILogWriter` and `ITestReport`, selected by type name in `Configs/UnitTestConfig.xml`. Formatting of individual lines (`Test.StatusMessage`) is fixed; you customise the sink (`ILogWriter`, receives already-formatted strings) and get a structured post-run hook (`ITestReport`, receives the full `ITestGroup` tree). There are no output templates or XML defs.

Sketch of a sink that mirrors DevTools output into `Player.log` with markers so the existing RimTest-style `until grep -q ... Player.log` flow works _(untested)_:

```csharp
internal sealed class PlayerLogWriter : ILogWriter
{
    public PlayerLogWriter(Logger logger)
    {
        // Logger drops every message unless this file exists.
        if (!File.Exists(logger.LogConfig.FullPath))
            File.WriteAllText(logger.LogConfig.FullPath, string.Empty);
    }
    public void PostInit() => Log.Message("[DevTools] TESTING START");
    public void Flush() { }
    public void WriteLine(string message) => Log.Message("[DevTools] " + message);
    public void Dispose() => Log.Message("[DevTools] TESTING END");  // runs when the runner finishes
}
```
with `<writer>MyMod.Tests.PlayerLogWriter</writer>`. Failed lines also reach `Log.Message` (not warnings), so they do not trip the failure watcher. `Dispose` runs on runner completion (`DevLog.DisableLogger` in a `using`), also when the run throws, but not on a hard crash.

### Recommended grep-able approach (no custom code)

1. Launch with `--pid <packageId> --test --exit` (see below), redirect nothing; the game exits when done.
2. Wait on the end line (no sleeps or timeouts): `until grep -q "Test runner finished. Result:" "<folder>/Test.log" 2>/dev/null; do sleep 2; done`. `gabs-launch.sh` deletes the old `Test.log` before launching, so the previous run's end line can't satisfy the wait.
3. Pass/fail: `grep -c "^\[Failed\]" Test.log` (fixture-level failures) and `grep -P "^--\t\[Failed\]" Test.log` (failed tests); `grep -P "^--\t\[Passed\]" Test.log | wc -l` for passes; the final `Test runner finished. Result: Passed|Failed` line; game exit code (1 = failures). Read the failing lines plus the following stack-trace lines for details.
4. Also grep `Player.log` for `Unable to register` / `must have 1 public constructor` / `Unable to fetch parameters` (discovery errors are `Log.Error` in `Player.log`, and a run with zero discovered fixtures produces no `Test.log` results at all: absence of `[Passed]`/`[Failed]` lines with a finished game means nothing ran).
5. Without `--exit`, the runner ends by opening the Test Explorer window and the game keeps running; the end-of-run line is then **not** written, so only per-fixture lines exist and you cannot detect completion from the log alone. Use `--exit`, a custom `ILogWriter`, or a `MarkdownReport`.

Cases where the summary/end line is missing: run without `--exit`; runner coroutine aborted by an unhandled exception (fixture constructor throws, scene-change exception not routed to `FailTestWhileTransitioning`); game crash or hang; log file over `maxFileSize`; `TestPlan`/`Smoke` managers behave the same for the end line but generate no report.

### Verified workflow (GABS launch inputs; RealisticOrbitalTrade, 269 tests)

`gabs-launch.sh` (this repo) supports it; the GABS game config declares the launch inputs (GABS >= 1.1.0; `games_show` lists the declared inputs):

```json
"launchInputs": {
  "runTests":   { "description": "Run the DevTools test suite, then quit the game", "type": "boolean", "args": ["--test", "--exit"] },
  "testFilter": { "description": "DevTools filter expression, e.g. class == WithCountTests", "type": "string",
                  "pattern": "[A-Za-z0-9_.=!~&| -]+", "args": ["--where", "${value}"] },
  "testPlan":   { "description": "Run a DevTools test plan (TestPlans/<name>.xml), then quit the game", "type": "string",
                  "pattern": "[A-Za-z0-9_.-]+", "args": ["--test-plan", "${value}", "--exit"] },
  "rimworldVersion": { "description": "RimWorld version to build and launch", "type": "string",
                  "pattern": "1\\.[0-9]+", "args": ["--rimworld-version", "${value}"] }
}
```
`rimworldVersion` is optional; without it `gabs-launch.sh` uses `RIMWORLD_VERSION` from `.vscode/build_config.sh` (default `1.7`).
Start with the GABS MCP tool: `games_start {"gameId": "<game-id>", "launchInputs": {"runTests": true, "testFilter": "class == WithCountTests"}}` (`testFilter` optional; the result's `appliedLaunchInputs` confirms them, and `started_bridge_pending` is expected).
- `gabs-launch.sh` forwards those args to the game; when `--test` is present it adds `--pid <packageId from About/About.xml>`, writes `<installed mod folder>/Configs/UnitTestConfig.xml` (the project's own `Configs/UnitTestConfig.xml` if present, plus `<log><folder>.savedatafolder/<version></folder></log>`), and deletes any old `Test.log`. On 1.7 it also adds `-NoWindow`, which DevTools treats as `--exit` plus `BatchModeCompatibility`. Results: `.savedatafolder/<version>/Test.log`, next to `Player.log`.
- `gabs-launch.sh` deletes the old `Test.log` before launching, and `games_start` returns only once the game is running, so waiting on `Test.log` is safe without deleting any log yourself (gabs.md, 'Never delete, truncate, move or rename `Player.log` or `Test.log`'): `until grep -q "Test runner finished. Result:" .savedatafolder/<version>/Test.log 2>/dev/null; do sleep 2; done` (backgrounded; it never matches if the run crashed, so check `games_status`/`Player.log` if it seems stuck). Roughly 1-2 minutes end to end for ~270 tests.
- Read the result: last line `Test runner finished. Result: Passed|Failed`; counts via `grep -cP '^--\t\[Passed\]'` / `'^--\t\[Failed\]'` (tests) and `grep -c '^\[Failed\]'` (fixtures); failure detail follows each `[Failed]` line. With filters or `Test.Skip`, compare counts to the `[Test]` count in source.
- Verified in a live run: `Test.log` location override via `<log><folder>` works; `warningsAllowed`/`errorsAllowed` entries are regexes matched against the logged text (partial match is enough); `-savedatafolder` does not affect `Test.log`.

### Whitelisting expected log output

Any warning/error logged while a fixture runs fails it. Tests that intentionally trigger logging (e.g. an error path under test) opt out per test with `[WarningsAllowed]`/`[ErrorsAllowed]` (namespace `DevTools.Testing`; arguments are regexes, a partial match is enough):

```csharp
[Test]
[ErrorsAllowed("Tradeable lacks AnyThing")]
public static void ReturnsNullBoundsWhenTradeableHasNoThing() { ... }
```
Put the attribute on the test method that logs, not the fixture, so other tests in the fixture still fail on unexpected output. The exception is a fixture where every test triggers the same expected log (DebugAssistance's `PatchAttributeScannerTests`: each test scans the whole test assembly, which includes deliberately unresolvable patch classes); there the attribute goes on the fixture class, with a regex naming exactly those sources and a comment saying why. Do not use the global `warningsAllowed`/`errorsAllowed` config lists for this.

A hand-built scribe round-trip harness (`CustomStreamReaderScribeLoader`) must call `Scribe.loader.crossRefs.RegisterForCrossRefResolve(loadTarget)` after `loadTarget.ExposeData()`; otherwise `FinalizeLoading` logs `Not all loadIDs which were read were consumed` for every reference field. `FinalizeLoading` itself resolves cross references and runs post-load inits.

## Project setup used by RealisticOrbitalTrade

- `Source/<Mod>.Tests/<Mod>.Tests.csproj`: default `OutputPath` (`../../<version>/Assemblies`, from Common.props), so `<Mod>.Tests.dll` sits next to the mod assembly, and:
  ```xml
  <ProjectReference Include="$(ProjectDependenciesBasePath)\Third-Party\DevTools\DevTools\DevTools.csproj"
      AdditionalProperties="RimWorldVersion=$(RimWorldVersion);OutDir=$(MSBuildThisFileDirectory)..\..\$(RimWorldVersion)\Assemblies\">
      <CopyLocal>False</CopyLocal>
      <Private>false</Private>
  </ProjectReference>
  ```
  `AdditionalProperties` passes the version through and overrides DevTools' own `OutDir` (relative to the DevTools clone) so `DevTools.dll` is built into the mod's `<version>/Assemblies`. `ProjectDependenciesBasePath` defaults to `..\..\..` (the `RimWorld/` folder), so DevTools is expected at `RimWorld/Third-Party/DevTools`.
- On 1.7 (net10.0), a project reference compiles against the referenced project's reference assembly unless that project sets `<ProduceReferenceAssembly>false</ProduceReferenceAssembly>`. A mod that ILRepacks its dependencies needs that setting for its tests to see the merged types (DebugAssistance does this). Test code that writes Reflection.Emit assemblies to disk uses `PersistedAssemblyBuilder` on 1.7, since `AssemblyBuilderAccess.Save` is .NET Framework/Mono only (DebugAssistance's `FixtureAssemblyBuilder`).
- No `LoadFolders.xml` entry and no `IfModActive`: the assemblies are in the always-loaded `<version>/Assemblies`, which is gitignored. The `.sln` omits `Release|Any CPU.Build.0` for the test project, so Release/CI builds neither the test assembly nor DevTools, and neither ships. A Release restore of the `.sln` succeeds without a DevTools checkout, so CI needs no DevTools step.
- Test classes: `[TestFixture(TestType.MainMenu)] internal sealed class X` (`sealed` avoids CA1852). Prefer `.Count` over `.Count()` on lists (CA1829).
- Tests for an integration project (e.g. `<Mod>.SocialEXPFromTrade.Tests`): same reference, but the test assembly keeps its `Integrations/<X>/<version>/Assemblies` output so it loads only when that mod is active; DevTools.dll still lands in `<version>/Assemblies`.

## Launching tests

- **In-game UI**: enable dev mode, open the debug-windows row; the "DevTools" icon (`TexButton.OpenStatsReport`) opens a mod/tool menu, then "Test Explorer". The window (`onlyDrawInDevMode`) has Run All, and a dropdown: Run Failed / Not Run / Passed Tests, Run Until Failure, Clear All Test Results; right-click selection -> Run Selected; "Open Log" opens `Test.log`. Runs work from the main menu or in-game (the runner replaces the current game with generated ones and returns to the main menu at the end).
- **Command line** (`Testing/CommandRunner.cs`, parsed from `Environment.GetCommandLineArgs()` at startup):

| Arg | Meaning |
|---|---|
| `--pid <packageId>` | **Required.** Must equal the target mod's `PackageIdPlayerFacing` (case-insensitive), else nothing runs. Checked per running mod; the first match wins. A match runs unit tests even without `--test` |
| `--test` / `-t` | Run unit tests (all fixtures of that mod) |
| `--smoke-test` / `-s` | Run smoke tests |
| `--test-plan <name>` / `-p <name>` | Run a test plan (`<mod>/TestPlans/*.xml`, spawns a child game process). **An unknown name logs nothing and runs every plan's jobs** |
| `--test-list <file>` | Run only the unit tests whose `FullName` is listed in `<file>`, one per line |
| `--where <expr>` / `-w <expr>` | Filter (below) |
| `--exit` / `-e` | Quit with exit code 0/1 at the end |
| `-NoWindow` | 1.7 engine flag. DevTools also treats it as `--exit` and enables `BatchModeCompatibility` (pre-initialises `Verse.Text`) |
| `--port <port>` / `--session <id>` | Stream results over TCP (see "TCP result stream"); 1.7 only, 1.6 logs a warning |
| `--test-discovery=<packageId>` | Handled by `DevMod`, independent of `--pid`: once the game reaches the main menu, saves `ModsConfig`, writes the test manifest for that mod's unit tests and shuts the game down |

Note: `Application.Quit` is only called by `--exit` (or `-NoWindow`); `--test` alone runs everything and leaves the Test Explorer open. The `--config` argument was removed (commit `d1df300`).

- **Filter expressions**: `key op value [&& key op value ...]` (also `||`; evaluated left to right, no precedence, parentheses unsupported). Keys: `category`, `method`, `class`, `namespace`, `type` (`MainMenu`/`Playing`/`PostGameExit`), `status` (`Failed`, `NotRun`, ...), or any metadata key. Ops: `==`, `!=`, `=~` (contains), `!~`. Values are single tokens (`[\w\d_.\-]+`); quoted values keep their quotes in the token, so avoid quotes. Examples: `--where "class == WithCountTests"`, `--where "namespace =~ MyMod.Tests && type == MainMenu"`, `--where "class == FooTests || class == BarTests"`.
  - **The joiners are exactly `&&`, `||`, `and` or `or`.** A single `|` or `&` is not a token at all (the tokenizer in `ExpressionGenerator.cs` drops it), so `class == A | class == B` reads as `class == A class == B` and throws `ArgumentException: class` from `ExpressionGenerator.GetBoolean`. That happens in `DevHarmony.FinalizeInit`, before any test is queued: no `Test.log` is written and `--exit` never fires, so the game stays open and a wait on `Test runner finished` hangs forever. If `Test.log` doesn't show up, grep `Player.log` for `Exception thrown executing command line args`.
  - `class ==` matches the fixture's `Name`, `FullName` or assembly-qualified name; `method ==` matches `Method`, `Class.Method` or `Namespace.Class.Method`. `=~` is a substring match on the same strings.

- **Under GABS**: add the args to `GABS_LAUNCH_ARGS` in the project's `.vscode/build_config.sh` (forwarded by `rimworld-utils/gabs-launch.sh` to the game), e.g. `GABS_LAUNCH_ARGS=(--pid ilyvion.RealisticOrbitalTrade --test --exit)`. Then wait as in "Recommended grep-able approach" (with `Test.log` instead of `Player.log`), or use a custom `ILogWriter` to emit markers into `Player.log`. Prefer setting `<log><folder>` to a directory the Flatpak sandbox can write (the launcher grants `--filesystem=$PROJECT_DIR`). The test mod and DevTools must be enabled in `ModsConfig.xml` (host mod carries DevTools.dll, so no separate mod). Because `--exit` quits the game, do not use `--exit` when the agent needs to keep driving the live game.

## Gotchas

- **Static fixture classes are skipped silently** and abstract classes too; a fixture needs one public instance constructor.
- **Debugging a failing run:** `Log.Message` output goes to `Player.log`, results to `.savedatafolder/<version>/Test.log`. Log state inside a tick loop only when it changes to see where a value diverges, and remove it before finishing. `Expect.*` doesn't throw, so one run can list several failures; `AreEqual(expected, actual)` prints `Expected <expected> == <actual>`.
- **Iterating:** run only the fixture you're working on (`testFilter: "class == <Fixture>"`), rebuild before each relaunch (the game loads the built mod), and stop the running game first. The runner exits when done, so `games_start` can report exit code 1 for a failing run; read the result from `Test.log`.
- **Logged warnings/errors fail tests** (default `failOnWarnings`/`failOnErrors` true; any `Log.Warning`/`Log.Error`/exception on any thread while the fixture/test runs, including from OneTimeSetUp and map generation). RimTest Redux captured them. For code under test that logs intentionally, put `[WarningsAllowed]`/`[ErrorsAllowed]` on that test (see "Whitelisting expected log output").
- `Expect` failures do not stop the test; keep guarding null-derefs yourself (`Expect.IsNotNull(x); if (x == null) return;`). `Test.Skip`/`Cancel` also do not stop the test.
- **Only the first unwhitelisted log message is reported** per failing test. A test that logs several distinct unexpected messages shows one per run; grep `Player.log` for all of them before whitelisting.
- **Name clash with Harmony:** `DevTools.Testing.MethodType` makes `MethodType` ambiguous (CS0104) in test files that also use `HarmonyLib.MethodType` (e.g. `[HarmonyPatch(typeof(T), MethodType.Getter)]`); add `using MethodType = HarmonyLib.MethodType;`.
- Exceptions thrown by test methods are caught and recorded as failures with the unwrapped inner exception; exceptions in fixture construction or `TestType` transitions are not caught locally and can kill the run without an end line.
- Parameter limits: at most one parameterised argument per test method/constructor; parameters without a source attribute log an error and the method is not registered (except `[TestCase]`).
- `[SetUp]`/`[TearDown]` must return `void`; setups run before every test on the shared fixture instance (fixture fields persist across tests of one run; re-run to get a new instance).
- Ordering: fixtures grouped by `TestType`, then `[ExecutionPriority]`, otherwise dictionary insertion order (assembly type order); tests likewise. With filters, fixtures are ordered by assembly full name (ordinal) first, then as above (`ExpressionTree.GetFilteredTests`).
- Main-thread only. The whole run is one Unity coroutine; long synchronous tests freeze the game (`TestFixtureManager` summary). Map generation for `Playing` fixtures (150x150) happens on every fixture that customises game settings; group tests to minimise regenerations.
- Tests run in a live game: static state, Harmony patches, defs and `Rand` persist. `Rand` is seeded deterministically per run (`RandBlockPersistent`). Use `ScopedMethodHook` for temporary Harmony patches (`Harmony.Patch`/`Unpatch` on dispose), `ScopedReferenceRollback`/`ScopedValueRollback` for temporary field values, `MockGameTicks`/`MockAbsTicks` for tick counters. `TickObserver<T>` and `TickIntervalObserver<T>` install a permanent Harmony postfix per entity type in their static constructor.
- `StackTraceCacheDisabler` (used by the runner) reflects on `HarmonyMod.Settings.noStacktraceCaching`; requires the Harmony mod to be loaded.
- DevTools compiles assertions via `UnityEngine.Assertions` internally; `Assert.raiseExceptions` behaviour in the player means non-throwing `Assert` calls in tests can only surface as logged `Assert` errors (which then fail the test via the log watcher).
- `Test.log` is not `Player.log`; a RimTest-style `TESTING END` wait will hang forever unless a custom writer emits it.
- Two DevTools runs at once (multi-instance) contend for `Test.log` via named mutexes; the non-owner appends. This is also what makes a multi-job test plan's children safe to run one after another into a single combined `Test.log` — see the `--test-plan` bullet below.
- `--pid` must match; forgetting it produces no error, the game simply starts normally.
- Dev-mode UI entry only appears when dev mode is on; command-line runs need nothing.
- **`--test-plan` in agent sessions: works through GABS; use it only when a test needs a different mod list or process, otherwise prefer the normal `runTests` flow.** A plan job (`Testing/Plan/TestProcess.cs`, `TestPlanManager.cs`) starts a second game process and can rewrite the mod list. Verified in a live run through `games_start` with a `testPlan` launch input (args `--test-plan ${value} --exit`): the parent spawned the child, the child passed, both exited, and `ModsConfig.xml` was restored.
    - *Launcher requirements.* DevTools only acts when `--pid <packageId>` is present; `gabs-launch.sh` injects it for `--test`/`-t`, `--test-plan`/`-p` and `--smoke-test`/`-s`, and writes the matching `Configs/<Unit|TestPlan|SmokeTest>Config.xml`. The plan XML lives in `<mod>/TestPlans/*.xml`; the project's `build_config.sh` must list `TestPlans` in `EXTRA_FILES` so the dev build copies it into the installed mod.
    - *ModsConfig.xml is rewritten and restored only on a clean finish.* If the job sets `loadWithMods`, the parent calls `ModsConfig.SaveFromList(...)` on the live config before spawning and restores the original list in `OnTestRunnerEnd`. If the game is stopped or killed mid-plan (`games_stop`/`games_kill`, a timeout, a crash) the instance keeps the plan's mod list. `gabs-launch.sh` keeps timestamped backups (`.savedatafolder/<version>/Config/ModsConfig.xml.bak-*`, newest 5, written when the content changed) at the start of every test launch; restore by copying the wanted one over `ModsConfig.xml`.
    - *Child launch arguments.* The child is started with `--pid <host> -e --child-index <N>`, the parent's `-savedatafolder`, a `-logfile` named `<parent log name>-child<N><ext>` (e.g. `Player-child0.log`, `Player-child1.log`, ...) and the parent's `-NoWindow` (each only when the parent has it), then the job's `commandLineArgs` (filter with `--where`; `--filter` is not an argument and is ignored, so the child runs the whole suite), then `-ColdBoot` (the parent holds locks on the texture cache files); arguments the job sets itself are not overridden. The executable is `Environment.ProcessPath` (on 1.6, the current process's main module). `N` is the job's zero-based index within `TestPlan.jobs` (`TestPlanJob.childIndex`, assigned by `TestPlanManager`), so a plan with several jobs gives each child its own `Player-child<N>.log` instead of every child clobbering a single shared `Player-child.log`. It inherits the environment and Flatpak sandbox (including `HARMONY_LOG_FILE` and the `GABP_*` variables). `DEVTOOLS_TEST_CHILD=1` is added to its environment.
    - *Two game instances at once.* The child is a full second RimWorld process, doubling memory and load time; on 1.7 it has no window (inherited `-NoWindow`), on 1.6 it opens a minimized window. Every `--pid` run forces `Application.runInBackground` on (`RunInBackground`, overriding the "run in background" preference), so neither the parent nor a child stops updating while its window is unfocused. A child that loads RimBridgeServer would try to serve the parent's GABP port _(unverified)_. GABS tracks only the parent. The parent polls until the child exits and kills it after `timeOut` (default 10 minutes), reporting a failure. The child's own game log is `Player-child<N>.log` (see above).
    - *`Test.log` is shared and combined by design, not clobbered.* Parent and every child write to the same `Test.log` in the same folder, but this is safe: `TestRunner.TestRoutine` opens the plan's `DevLog.Enabler`/`Logger` **once, before the `foreach` loop that runs the plan's jobs**, and keeps it open (as the mutex owner, `FileMode.Create`) until every job has finished. Each child starts a fresh process later, so its own `Logger` construction always loses the non-blocking `ownerMutex.WaitOne(0)` race to the still-running parent and falls back to `FileMode.Append` (`Logger.cs`/`TextLogWriter.cs`) — no child ever truncates the file mid-plan. Since jobs run one at a time, the end result is every job's results appended in order into one combined `Test.log` for the whole plan, which is the desired behavior (one file/list for the whole plan's outcome) rather than a bug.
    - *Waiting on plan completion: use `"Test plan finished. Result:"`, not `"Test runner finished. Result:"`.* `TestPlanManager.OnTestRunnerEnd` writes its own distinct `Test plan finished. Result: Passed|Failed` line for the plan's overall result, precisely because a job can itself run a nested unit-test suite (`--test --exit` in its `commandLineArgs`) whose child process writes its own ordinary `Test runner finished. Result:` line into the same shared `Test.log` partway through the plan. A wait loop written for a single `--test` run (`grep -q "Test runner finished. Result:"`) would match on that first child's line instead of the plan actually finishing; for a plan, wait on `grep -q "Test plan finished. Result:"` instead, which is written once, by the parent, only after every job has completed.

## Helper types

All public unless noted.

| Type | Signature / use |
|---|---|
| `Test` (static) | `Test.Current`, `Fail(string, string=null)`, `Fail(Exception)`, `Skip(string=null)`, `Cancel(string=null)`, `IEnumerator Suspend(float secondsTimeOut, string message=null)`, `GetGroups(ITestManager)`, `ResetAll(ITestManager)` |
| `TestRunner` | `new TestRunner(ITestManager[, TestFilter | ExpressionTree])`, `Run()`, `AddStopCondition(StopOnTest)`, `AddPreTestAction/AddPostTestAction(TestAction)`, `AddTestActions(ITestActions)`, `ConnectTcp(int port, string sessionId)` (1.7 only), `SignalToStop()`, `static Active`, `static Current`, `static event Action<bool> OnTestRunnerStateChange` |
| `TestFixtureManager` | `Config`, `RunAll()`, `OpenMenu()`; extensions in `Ext_TestManagers`: `OpenLogFile()`, `GetRunnerWith(filter | ExpressionTree | Expression, Comparison, string)`, `LoadConfig<T>(mod)`, `SaveMetaData()`; static `Ext_TestManagers.FindTestPlan(name, mod)` |
| `WaitTicks(int ticks)` | `CustomYieldInstruction` over `TicksGame` |
| `WaitUntilTimeout(Func<bool> predicate, int maxTicksToWait)` | fails the test with "Timed out" |
| `WaitJob(Pawn, JobDef, int timeoutTicks = 2500, WaitJobType = StartsAndFinishesJob)`; `pawn.WaitJob(...)` extension | waits for a job |
| `MapUtils.KillEverything(Map)` | destroys all pawns on a map |
| `ScopeEntity(Thing)`, `ScopeWorldObject(WorldObject)`, `ScopeWindow(Window)` | `using` structs that destroy/close on dispose |
| `MockGameTicks(int)` / `MockAbsTicks(int)` | temporary `TicksGame` / `gameStartAbsTick` |
| `RandBlockPersistent(uint seed)` | seed that survives frames |
| `ScopedMethodHook(MethodInfo original, HarmonyMethod prefix=null, postfix=null, finalizer=null)` | temporary Harmony patch |
| `ScopedReferenceRollback<T, F>(T instance, string fieldName[, F newValue])` | temporary field override (`T : class`) |
| `ScopedValueRollback<T>(ref T)` (`T : unmanaged`, unsafe) | restores value on dispose |
| `TickObserver<T>(T entity)`, `TickIntervalObserver<T>(T entity)` (`T : Entity`) | `.TickCount` of `Tick` / `TickInterval` calls |
| `GenStepWarningDisabler` | suppresses `GenStep_Scatterer` warnings during map generation |
| `StopOnDispose(Stopwatch)` | stopwatch scope |
| `MetaDataContainer` (`Get<T>(key)`), `ITestCase`, `ITestFixture`, `ITestFunction`, `ITestGroup`, `ITestManager`, `ITestConfig`, `ITestActions` | extension interfaces |
| `Expression`, `ExpressionTree`, `Expression.Comparison` | programmatic filters |
| `ILogWriter`, `ITestReport`, `Logger`, `Logger.Config`, `DevLog` (`Write`, `WriteLine`, `WriteVerbose`, `Flush`) | logging/reporting; `DevLog.Write` inside a running test appends to the test log |

## Source map (DevTools repo)

- Discovery/entry: `DevTools/DevHarmony.cs`, `DevTools/Testing/Test/TestFixtureManager.cs`, `DevTools/Testing/TestExtensions.cs`
- Run loop: `DevTools/Testing/TestRunner.cs`, `DevTools/Testing/Test.cs`
- Assertions: `DevTools/Testing/Expect.cs`, `DevTools/Testing/MessageBuilder.cs`
- Logging/reports: `DevTools/DevLog.cs`, `DevTools/Testing/Logging/*.cs`, `DevTools/Testing/LogWatcher.cs`
- CLI: `DevTools/Testing/CommandRunner.cs`, `DevTools/DevMod.cs` (`--test-discovery`)
- TCP results / test adapter: `DevTools/Testing/Test/TcpBridge.cs`, `DevTools/Testing/TestMessage.cs`, `DevTools.TestAdapter/`
- Config: `DevTools/Testing/ITestConfig.cs`, `DevTools/Testing/TestConfig.cs`, `DevTools/Testing/Ext_TestManagers.cs`
- Filters: `DevTools/Testing/Test/Expressions/*.cs`
- UI: `DevTools/Testing/Test/TestExplorer/*.cs`
