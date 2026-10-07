---
name: fix-17-warnings
description: "Fix the build warnings a mod's RimWorld 1.7 (net10.0) build produces once 1.7 support has been added: first nullability warnings (CS86xx), then CA1510/CA1512 (ArgumentNullException.ThrowIfNull / ArgumentOutOfRangeException.ThrowIf* throw helpers, shimmed for 1.6 and earlier), then IDE0330 (System.Threading.Lock, shimmed for 1.6 and earlier), then CA1305 on StringBuilder.Append with an interpolated string (shimmed for 1.6 and earlier), then stop and report what's left. Use when asked to fix, clean up or address 1.7 warnings, or the warnings left behind by update-to-17."
---

# Fixing RimWorld 1.7 build warnings

The 1.7 build targets `net10.0`, whose framework APIs carry nullable annotations, and its analyzers suggest APIs that only exist on .NET 7+. Every older version targets `net481`, which has neither. Each fix has to work for every version in `About/About.xml` `<supportedVersions>`: all versions keep building, and the test suite keeps passing.

Work in this order, and do not go past step 5:

1. Nullability warnings.
2. CA1510 and CA1512.
3. IDE0330.
4. CA1305 on `StringBuilder.Append`.
5. Stop. Report the remaining warnings and wait for the user's instructions.

Ask the user (AskUserQuestion) whenever something is unclear. Do not commit unless told to.

## 0. Baseline

- Do a clean build of every supported version. `.vscode/build.sh` does an incremental build, so run `dotnet clean <Mod>.sln -p:RimWorldVersion=<version>` before each `.vscode/build.sh <version>`. Pass `SKIP_MOD_FOLDER_REMOVAL=true` to every build after the first so the deployed mod keeps every version's output.
- Every version except 1.7 must already have 0 warnings. If one doesn't, tell the user before going on.
- Save the 1.7 warning list, grouped by warning code. That's the work list.

## 1. Nullability warnings (CS86xx)

Fix these in shared code, not behind `#if`. Don't silence them with `!`, `#pragma` or `NoWarn`.

- **Override or implementation doesn't match the annotated signature (CS8765, CS8767):** change the signature to match the framework's, e.g. `Equals(object? obj)` or `IComparer<TKey>? comparer`. Making a parameter nullable is also valid on net481.
- **A generic type is used as a `Dictionary` key (CS8714):** add `where TKey : notnull`.
- **An `out` value passed through from a framework `Try*` method (CS8601):** annotate it the way the framework does, e.g. `[NotNullWhen(true)] out TValue?` for a class-constrained `TValue`, or `[MaybeNullWhen(false)] out TValue`. This needs `using System.Diagnostics.CodeAnalysis;` in the file, because that namespace isn't a global using. PolySharp supplies the attributes on net481.
- **A framework member that net10 marks nullable (CS8600, CS8602, CS8603, CS8604):** add a defensive check that sends null down the code's existing failure path. Examples:
    - `curXmlParent.Attributes?["IsNull"]`
    - `node.OwnerDocument?.SelectNodes(...)`, with `matches == null ||` added to the "no matches" branch
    - skipping an `ArraySegment<T>` whose `Array` is null
    - `Assembly.GetName().Version ?? new Version(0, 0)`
    - `OfType<T>()` instead of `Where(x => x != null)`, so the compiler knows the elements aren't null
- Code that only compiles for older versions (`#if v1_3` etc.) produces no 1.7 warnings. Leave it alone in this step.

Rebuild all versions (clean) before moving on.

## 2. CA1510 and CA1512

The throw helpers `ArgumentNullException.ThrowIfNull` and `ArgumentOutOfRangeException.ThrowIfNegative`/`ThrowIfZero`/`ThrowIfGreaterThan`/... only exist on .NET 7+. On 1.6 and earlier they come from C# 14 static extension members (`extension(System.ArgumentNullException) { public static void ThrowIfNull(...) }`), compiled only when `!v1_7_OR_GREATER`. Call sites are written the same way on every version.

**Where the helpers come from:**

- **The mod has `<UseLaboratory>true</UseLaboratory>`:** ilyvion.Laboratory provides them as the public `ArgumentNullExceptionExtensions` and `ArgumentOutOfRangeExceptionExtensions`, visible through the `global using ilyvion.Laboratory;`. Don't add anything to the mod for this.
    - On 1.6 and earlier, the mod now needs a Laboratory release that includes them installed at runtime. Raise the `majorVersion`/`minorVersion` in the mod's `VersionCheckDef` (`Common/Defs/VersionCheck.xml`) to the first such release; `../ilyvion.Laboratory/CHANGELOG.md` shows which one that is. If they're still under `[Unreleased]`, ask the user which version to require.
- **The mod doesn't use Laboratory:**
    - Copy `ArgumentNullExceptionExtensions.cs` and `ArgumentOutOfRangeExceptionExtensions.cs` from `../ilyvion.Laboratory/Source/ilyvion.Laboratory/` into the mod. Change the namespace to the mod's root namespace and `public static class` to `internal static class`. Keep the `#if !v1_7_OR_GREATER` and the CA1034 `#pragma`.
    - Add tests that call the helpers the same way call sites do (`ArgumentNullException.ThrowIfNull(x)`). Base them on `../ilyvion.Laboratory/Source/ilyvion.Laboratory.Tests/ThrowHelperExtensionsTests.cs`. A successful compile doesn't prove extension members work at runtime; the 1.6 test run does.

**Converting call sites:**

- Convert every flagged site:
    - `if (x == null) { throw new ArgumentNullException(nameof(x)); }` becomes `ArgumentNullException.ThrowIfNull(x);`
    - `if (count < 0) { throw new ArgumentOutOfRangeException(nameof(count)); }` becomes `ArgumentOutOfRangeException.ThrowIfNegative(count);`
- Also convert the same pattern in code that only compiles for older versions, so every call site looks the same.
- Leave these alone, since the analyzer doesn't flag them:
    - throw expressions (`x ?? throw new ...`, `x == null ? throw new ... : ...`)
    - null checks on an unconstrained generic value, because `ThrowIfNull(object?)` would box it
    - throws with a custom message
- Never shim by declaring a type named `ArgumentNullException`/`ArgumentOutOfRangeException` (e.g. a subclass) or by aliasing the name to one. That shadows the `System` type in `catch`, `is`, `typeof` and `Expect.Throws<T>`, which then stop matching exceptions thrown by vanilla or the framework.

Rebuild all versions (clean) before moving on.

## 3. IDE0330

`System.Threading.Lock` only exists on .NET 9+. On 1.6 and earlier, a `Lock` class compiled only when `!v1_7_OR_GREATER` stands in for it: `lock` on it takes its `Monitor`, and it has the same `Enter`/`TryEnter`/`Exit`/`IsHeldByCurrentThread`/`EnterScope` members. Lock fields are declared as `Lock` on every version; on 1.7 the name resolves to `System.Threading.Lock` through the implicit `using System.Threading;`.

**Where the shim comes from:**

- **The mod has `<UseLaboratory>true</UseLaboratory>`:** ilyvion.Laboratory provides it as the public `ilyvion.Laboratory.Lock`. Don't add anything to the mod for this.
    - On 1.6 and earlier, the mod now needs a Laboratory release that includes it installed at runtime. Raise the `VersionCheckDef` as in step 2, to the first release that includes both the throw helpers (if step 2 used them) and `Lock`.
- **The mod doesn't use Laboratory:**
    - Copy `Lock.cs` from `../ilyvion.Laboratory/Source/ilyvion.Laboratory/` into the mod. Change the namespace to the mod's root namespace and `public sealed class` to `internal sealed class`. Keep the `#if !v1_7_OR_GREATER` and the CA2002 and CA1034 `#pragma`s.
    - Add tests based on `../ilyvion.Laboratory/Source/ilyvion.Laboratory.Tests/LockTests.cs`.

**Converting fields:**

- `private readonly object _lock = new();` becomes `private readonly Lock _lock = new();`.
- Also collapse existing `#if v1_7` / `#else` pairs that declare the same lock as `Lock` and `object`.
- A lock object that is also passed to `Monitor.*` or used as an `object` stays `object`. On 1.7 a `System.Threading.Lock` used that way gets a monitor separate from the one `lock` takes.

Rebuild all versions (clean) before moving on.

## 4. CA1305 on `StringBuilder.Append`

CA1305 on `sb.Append($"...")` suggests `StringBuilder.Append(IFormatProvider, ref AppendInterpolatedStringHandler)`, which only exists on .NET 6+. On 1.6 and earlier, an `Append(this StringBuilder, IFormatProvider?, ...)` extension method with its own `StringBuilderAppendInterpolatedStringHandler`, compiled only when `!v1_7_OR_GREATER`, stands in for it. Call sites are written the same way on every version.

**Where the shim comes from:**

- **The mod has `<UseLaboratory>true</UseLaboratory>`:** ilyvion.Laboratory provides it as the public `StringBuilderAppendExtensions`. Don't add anything to the mod for this.
    - On 1.6 and earlier, the mod now needs a Laboratory release that includes it installed at runtime. Raise the `VersionCheckDef` as in step 2, to the first release that includes every Laboratory shim the earlier steps used, and this one.
- **The mod doesn't use Laboratory:**
    - Copy `StringBuilderAppendExtensions.cs` from `../ilyvion.Laboratory/Source/ilyvion.Laboratory/` into the mod. Change the namespace to the mod's root namespace, and `public static class` and `public readonly struct` to `internal static class` and `internal readonly struct`. Keep the `#if !v1_7_OR_GREATER` and the IDE0060, CA1815 and CS9113 `#pragma`s.
    - Add tests based on `../ilyvion.Laboratory/Source/ilyvion.Laboratory.Tests/StringBuilderAppendExtensionsTests.cs`.

**Converting call sites:**

- `sb.Append($"...")` becomes `sb.Append(CultureInfo.InvariantCulture, $"...")`.
- Vanilla sets the current culture to en-US (`CultureInfoUtility.EnsureEnglish`), which formats numbers the same as the invariant culture. Where an interpolated `DateTime` or `DateTimeOffset` has no format string, pass `CultureInfo.CurrentCulture` instead, since the invariant culture formats dates differently from en-US.
- Other CA1305 sites, such as `ToString()`, `Parse` or `string.Format`, are not part of this step. Leave them for step 5.

Rebuild all versions (clean) before moving on.

## 5. Stop and report

- At this point, every version except 1.7 has 0 warnings and 0 errors. 1.7 has 0 errors, and its only warnings are ones other than nullability, CA1510, CA1512, IDE0330 and CA1305 on `StringBuilder.Append`.
- Don't fix, suppress or `NoWarn` the remaining warnings. List them for the user grouped by code, with counts and `file:line`, and wait for their instructions.

## Verify

- `csharpier format .`
- Run the test suite on 1.6 (see `gabs.md`). It's the regression check for the null checks and, when the mod has its own copy of the helpers or `Lock`, the runtime check for them and for the `StringBuilder.Append` shim.
- Players can't see any of these changes, so they don't need a changelog entry.
- Summarize for the user:
    - the nullability fixes, by kind
    - the number of throw-helper, `Lock` and `StringBuilder.Append` conversions
    - where the helpers, `Lock` and the `StringBuilder.Append` shim come from, and any `VersionCheckDef` change
    - the remaining 1.7 warnings
