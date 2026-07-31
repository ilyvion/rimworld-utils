Tests for a mod live in a sibling `<ModName>.Tests` project (e.g. `Source/ColonyManagerRedux.Tests`) and run in-process, in-game, via the sibling **RimTest Redux** mod (`../RimTest`, project `RimTestRedux.csproj`). This project is dev-only:

To actually execute the suite, see [gabs.md](gabs.md)'s test-verification procedure (build, launch under GABS, watch `Player.log` for RimTest Redux's `TESTING START`/`TESTING END` markers) — this file only covers writing tests.

- It should be listed in the mod's `.sln` so it always builds locally (default `Debug` config), but the `.sln`'s `ProjectConfigurationPlatforms` section shoul deliberately omit a `Release|Any CPU.Build.0` line for its GUID, so a `Release` build (what CI/release always uses) skips it entirely and its output never ships. Don't "fix" this by adding the missing line.
- **Only after adding or moving projects/references**, verify the split still holds: delete the built `<ModName>.Tests.dll`, build `Release` and confirm it stays absent, then rebuild `Debug` to restore normal dev state.

**Test suite mechanics**: a suite is a `static class` marked `[TestSuite]`; each test is a `static void` parameterless method marked `[Test]`. Assertions use a fluent API with interchangeable no-op grammar links (`.Is`, `.Not`, `.Has`, `.Does`, etc.) — `Assert.That(IComparable)` for scalars, `Assert.ThatCollection(IEnumerable)` for collections (real methods include `Has.Count(int)`, `Does.Contain(object)`, `Is.Empty()` — don't guess at the API; grep `RimTestRedux/Testing/Assert.cs` in the sibling repo for the real surface before relying on an assertion you haven't seen used elsewhere in the mod).

To unit test logic that's entangled with game/presentation logic:

1. Extract the pure, side-effect-free branching/comparison logic out of the method into its own `internal static` method (generic over plain types/delegates instead of live game types where possible), leaving the original call site to call it.
2. Grant the test project visibility: add `[assembly: InternalsVisibleTo("<ModName>.Tests")]` to the project the logic lives in (see existing `Core/InternalsVisibleTo.cs`-style files for the pattern) and add that project as a `ProjectReference` in the `.Tests.csproj` if it isn't already there.
3. Before writing assertions against an actual RimWorld/Verse type's constructor or API shape, verify it rather than guessing — e.g. inspect `~/.nuget/packages/krafs.rimworld.ref/<version>/ref/net472/Assembly-CSharp.dll` via reflection metadata, or grep the mod/siblings for existing working usages.

Prefer test names and bodies that state the behavior being guarded (e.g. `FailedSwapRestoresOriginalContents`), and where a test exists specifically because of a past bug, say so in a comment so a future change doesn't silently regress it.

Every code change must be accompanied by one or more tests. If the code being modified has no existing test coverage, write a passing test that captures the current behavior _before_ making any changes — this acts as a regression guard. Then make the change and add or update tests to cover the new behavior.
