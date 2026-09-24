## Harmony is a MOD dependency, not a NuGet runtime dependency

The canonical RimWorld pattern (since ~2020): your mod depends on Brrainz's "Harmony" mod (packageId `brrainz.harmony`, Steam Workshop id 2009463077). That mod loads `0Harmony.dll` once into the AppDomain before any user mod runs. Your mod compiles against the `Lib.Harmony` NuGet package purely for type signatures and DOES NOT ship its own `0Harmony.dll` (`Common.props` references it with `ExcludeAssets=runtime` and picks the version per RimWorld version).

**About.xml:**
```xml
<modDependencies>
  <li>
    <packageId>brrainz.harmony</packageId>
    <displayName>Harmony</displayName>
    <steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl>
    <downloadUrl>https://github.com/pardeike/HarmonyRimWorld/releases/latest</downloadUrl>
  </li>
</modDependencies>
<loadAfter>
  <li>brrainz.harmony</li>
</loadAfter>
```

*Why not ship `0Harmony.dll` yourself:* if two mods both ship Harmony at slightly different versions (very common — every release of `Lib.Harmony` is a new version token), Mono's assembly resolver picks one and the other mod's patches die at boot with `TypeLoadException`. Brrainz's mod centralizes this so there's exactly one Harmony in the AppDomain.

## Tick-driven map-scoped logic = `MapComponent`, not `GameCondition` or Harmony

Subclass `MapComponent`, override `MapComponentTick()`, and RimWorld auto-discovers it — no XML, no DefOf, no Harmony patch needed.

*Why it's tricky:* docs and examples lean toward `GameCondition` for time-bound effects and toward Harmony for "every tick do X". `MapComponent` is the right answer for "always-on while a settings toggle is true" and is the lowest-friction option.

## When patching a `Thing` base method and you need a comp, cast to `ThingWithComps` first

`Thing.GetComp<T>()` does not exist — comps live on `ThingWithComps`. In a Harmony postfix on a `Thing` base method, pattern-match before calling `TryGetComp`:

```csharp
if (!(__instance is ThingWithComps twc)) yield break;
var forbid = twc.TryGetComp<CompForbiddable>();
if (forbid == null) yield break;
```

CS1061 `'Thing' does not contain a definition for 'GetComp'` is the giveaway.

*Why it's tricky:* most tutorials show `thing.GetComp<>()` because the example is already a `ThingWithComps`. When you patch the base `Thing.GetGizmos`, the parameter type is the parent and the extension method silently doesn't apply.

## When computing GUI-space distance in a Harmony prefix, use Event.current.mousePosition — never convert Input.mousePosition manually

Inside a Harmony prefix on any method called during `OnGUI()`, `Event.current.mousePosition` gives the mouse position in the correct GUI coordinate space — already accounting for `GUI.matrix`, UI scale, and scroll-view offsets. Computing distance between a `Rect` parameter and `Event.current.mousePosition` in the same prefix is always coordinate-correct.

Do NOT try to reproduce GUI space from `Update()` by flipping `Input.mousePosition` with `Screen.height - y`. That formula breaks whenever RimWorld's UI scale (`Prefs.UIScale`) is not 1.0 — the scaled GUI units don't match raw screen pixels, so button rects and the mouse end up in different spaces and the distance is wrong.

Pattern for caching GUI-space proximity across the Update/OnGUI boundary:
1. In the Harmony prefix (during OnGUI), compute the distance and store it in a static field (`float MinDist`).
2. Reset that field once per Unity frame using a `Time.frameCount` guard in a separate `UIRoot.UIRootOnGUI` prefix.
3. Read `MinDist` from `GameComponentUpdate()` — it's one frame behind, which is imperceptible.

## When patching overloaded methods, list every parameter type — and use TargetMethod for out/ref parameters

`Widgets.ButtonText` and many other vanilla helpers have multiple overloads. Using `[HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonText))]` without a `Type[]` argument throws at static-ctor time:

```
HarmonyException: Ambiguous match for HarmonyMethod[(class=Verse.Widgets, methodname=ButtonText, type=Normal, args=undefined)]
```

Always include the parameter type array for any method that's overloaded:

```csharp
[HarmonyPatch(typeof(Widgets), nameof(Widgets.ButtonText),
    new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(Color), typeof(bool), typeof(TextAnchor?) })]
```

The array must **exactly equal** the overload's parameter list, including optional parameters that have defaults. `LetterStack.ReceiveLetter`'s "letter" overload is `ReceiveLetter(Letter let, string debugInfo = null, int delayTicks = 0, bool playSound = true)`; `new[] { typeof(Letter), typeof(string) }` matches **zero** overloads because the optional `int`/`bool` parameters are still part of the signature. The result is:

```
HarmonyLib.HarmonyException: Patching exception in method null
---> System.ArgumentException: Undefined target method for patch method static System.Void MyMod::Patch_X(...)
```

Find the full signature with the decompiler MCP (`list_members` / `get_member_signature`) and list every parameter type:

```csharp
[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
    new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
```

Same trap: `IncidentQueue.Add(QueuedIncident)` vs `Add(IncidentDef, int, IncidentParms, int)` — pick the right one AND list all its types.

### out / ref parameters

The attribute form `[HarmonyPatch(Type, string, Type[], ArgumentType[])]` (declaring `ArgumentType.Out` / `Ref`) is documented but unreliable in practice: it compiles, but `harmony.PatchAll()` can still throw `Undefined target method for patch method ...`. Use `static MethodBase TargetMethod()` instead, which lets you build the parameter-types array at runtime with `MakeByRefType()`:

```csharp
[HarmonyPatch]
public static class Patch_RecruitSucceed
{
    static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(InteractionWorker_RecruitAttempt),
            "DoRecruit",
            new[] {
                typeof(Pawn), typeof(Pawn),
                typeof(string).MakeByRefType(), typeof(string).MakeByRefType(),
                typeof(bool), typeof(bool)
            });
    }

    public static void Postfix(Pawn recruiter, Pawn recruitee) { ... }
}
```

`AccessTools.Method` consumes the by-ref types correctly and resolves the overload unambiguously. Use the same pattern for `ref` params (also `MakeByRefType()`). It also sidesteps `typeof(T).MakeByRefType()` being a non-constant expression (CS0182) inside attribute args.

*Why it's tricky:* the patch attribute compiles fine, and the failure is thrown by Harmony during `PatchAll`, before any of your patches run. It kills your entire mod's patch class at load — the user sees a `TypeInitializationException` on your mod's HarmonyPatches static constructor (or a `TargetInvocationException` from the mod constructor) with no clear pointer to which patch is at fault; look near the bottom of the stack for the original method name. One failed patch aborts the WHOLE class's `PatchAll`, so a second wrong signature later in the same class never gets to throw its own error — you fix the first one only to get the second on relaunch.

> **Unverified:** The claim that the `[HarmonyPatch(..., ArgumentType[])]` attribute form fails to bind on RimWorld was not re-tested. Check this against the decompiler/vanilla defs before relying on it.

## When recording layout rects of a recursive method, use Prefix not Postfix or order is reversed

If you're patching a method that recurses into itself (or into helpers that re-enter it) — for example `Listing_TreeThingFilter.DoCategory` which calls `DoCategoryChildren` which calls `DoCategory` again for sub-categories — and you record layout/cell rects in a Postfix, the recorded order is REVERSED relative to draw order. The Postfix on the outer call runs AFTER the inner-call Postfixes have already run, so children appear at LOWER indices than their parent in your captured list. Any "walk back to find the enclosing parent" logic then silently finds the wrong sibling instead of the actual parent.

Recipe: record the rect in a Prefix (capture `curY` / whatever positional state you need before the original method draws and recurses). Use a Postfix only for state that must be observed AFTER children — e.g. the final advanced `curY`. Captured rect Y is identical either way (Prefix `curY` IS the row's top-left).

*Why it's tricky:* the rect's Y coordinate looks correct in both Prefix and Postfix (same `curY` snapshot), so visual highlighting works fine. The bug only surfaces when other code does index-based traversal of the captured list — and the symptom (highlight jumps to a sibling, parent isn't collapsed) doesn't obviously point at "your patch fires in the wrong order".

## GameComponent.GameComponentUpdate does NOT fire in Entry state — patch UIRoot_Entry.UIRootUpdate for new-game / main-menu input

`GameComponent.GameComponentUpdate` only fires when `Current.Game != null` — i.e. once you're inside a loaded game (ProgramState.Playing). The entire `ProgramState.Entry` phase (main menu, scenario picker, storyteller picker, Page_CreateWorldParams, Page_ConfigureStartingPawns, etc.) runs WITHOUT GameComponent ticks. Code there must hook `UIRoot_Entry.UIRootUpdate` instead.

Recipe: Postfix-patch `UIRoot_Entry.UIRootUpdate` and drive your input handlers from there. It's a once-per-frame Update-phase callback in Entry state (parallel to `UIRoot_Play.UIRootUpdate` which handles Playing state).

*Why it's tricky:* Removing the `if (Current.ProgramState != ProgramState.Playing) return;` from `GameComponentUpdate` looks like it should let it run in Entry — it doesn't, because the engine simply never calls it when there's no `Current.Game`. The fix is at the patch site, not the gate.

## Harmony version mismatch between your build and the runtime kills every def in the mod

RimWorld's Mono refuses to bind a typeref like `HarmonyLib.CodeInstruction` compiled against one `0Harmony` version to a loaded `0Harmony` of a different version — even though both are weakly-named. The result is a `ReflectionTypeLoadException` on `Assembly.GetTypes()` that kills your entire mod assembly. RimWorld swallows the exception silently and you only see it as a flood of `Type X is not a Def type or could not be found` errors for every def in your Defs/ folder, plus one easy-to-miss `ReflectionTypeLoadException getting types in assembly X` line in Player.log.

*Why it's tricky:* the mod works until the Lib.Harmony version it was compiled against drifts from the `0Harmony.dll` that `brrainz.harmony` loads, and then **every def in your XML stops loading** with what looks like a totally unrelated XML/type error. `grep -B2 -A30 "ReflectionTypeLoadException getting types"` on Player.log is the fastest way to surface the real cause. Don't rely on `PublicKeyToken=null` "weak binding" to paper over version drift; Mono enforces the version-qualified typeref strictly here. The compiled-against version comes from `Common.props`.

## Patching all overrides of an abstract method (e.g. PawnColumnWorker.DoCell across DLC/mod subclasses)

When you want to intercept calls to an abstract or virtual method across every subclass — including those defined by DLCs and other mods you don't know about at compile time — Harmony's standard `[HarmonyPatch(typeof(BaseClass), nameof(BaseClass.Method))]` does NOT work. Patching the abstract method itself does nothing because virtual dispatch goes directly to the override, never touching the abstract MethodInfo. You have to enumerate the subclasses at runtime and patch each override:

```csharp
[StaticConstructorOnStartup]
static class MyDynamicPatcher
{
    static readonly Harmony _harmony = new Harmony("yourmod.dynamic.YourPatchName");

    static MyDynamicPatcher()
    {
        var postfix = new HarmonyMethod(typeof(MyDynamicPatcher), nameof(MyPostfix));
        foreach (var type in typeof(PawnColumnWorker).AllSubclassesNonAbstract())
        {
            var method = AccessTools.Method(type, nameof(PawnColumnWorker.DoCell),
                new[] { typeof(Rect), typeof(Pawn), typeof(PawnTable) });
            if (method == null) continue;
            // CRITICAL: only patch types that DECLARE their own override.
            // Otherwise you'd patch the same inherited MethodInfo multiple times.
            if (method.DeclaringType != type) continue;
            try { _harmony.Patch(method, postfix: postfix); }
            catch (Exception ex) { Log.Warning($"Patch failed for {type.FullName}: {ex.Message}"); }
        }
    }
}
```

Key points:
- `[StaticConstructorOnStartup]` runs AFTER every mod assembly is loaded, so `GenTypes.AllSubclassesNonAbstract` (a.k.a. the extension method on `typeof(T)`) sees subclasses from every active DLC and mod.
- Use a SEPARATE Harmony instance with a unique ID so you don't conflict with the main `[HarmonyPatch]` attribute-driven patches.
- Filter on `DeclaringType != type` — otherwise a subclass that inherits the override (rather than redeclaring it) would resolve `AccessTools.Method` to the parent's MethodInfo, and you'd patch it twice (once from the parent's iteration, once from the child's).
- Wrap each `_harmony.Patch(...)` call in try/catch so one mod's weird subclass (e.g. abstract intermediary, generic type) doesn't break the rest.
- Wrap the patch body in try/catch and silently swallow — never break vanilla's per-cell drawing for a mod that's just trying to peek.

*Why it's tricky:* Harmony's documentation makes attribute-driven `[HarmonyPatch(typeof(X), …)]` feel like the only path, and "patch the abstract base method" looks like the obvious approach for catching all overrides. But abstract methods have no body to inject into — they only exist as a method table entry — so the Postfix never runs. The fix is dynamic at startup: iterate subclasses and patch each leaf override, which is also how you reach DLC/mod-added classes you don't reference at compile time.

## Don't patch generic methods or methods of generic classes when T is a reference type — shared JIT codegen breaks every other instantiation

Harmony 2.x cannot patch an open generic method definition: passing `typeof(X).GetMethod("Foo")` (the open definition) to `[HarmonyPatch]` silently fails to bind with "Patch target not found", which looks like version drift. You have to close it first, via `MakeGenericMethod(...)` for a generic method or `AccessTools.Method(typeof(GenericClass<Arg>), "Name")` for a method on a generic class. For reference-type arguments, that is where the real problem starts.

In .NET, generic code instantiated over reference types shares one JIT'd machine-code body across all such instantiations. Harmony attaches the patch to that shared body, so the patched prologue runs for **every** reference-type instantiation, but its parameter capture/cast logic is typed to the one you closed over:

```csharp
var m = openGeneric.MakeGenericMethod(typeof(TabRecord));
harmony.Patch(m, postfix: ...);   // also runs for DrawTabs<ResearchTabRecord>, DrawTabs<InfoCardTab>, ...
```

When the actual call uses `T = ResearchTabRecord`, the cast `List<ResearchTabRecord>` → `List<TabRecord>` throws `InvalidCastException`.

The same applies to patching a method on a closed generic class. Patching `Dialog_ManagePolicies<DrugPolicy>.DoPolicyListing` and `Dialog_ManagePolicies<ApparelPolicy>.DoPolicyListing` (four separate `[HarmonyPatch]` classes, each calling `AccessTools.Method(typeof(Dialog_ManagePolicies<TPolicy>), "DoPolicyListing")`) produces thousands per frame of `ArrayTypeMismatchException: Attempted to access an element as a type incompatible with the array` in `Exception filling window for Dialog_ManageApparelPolicies`. The patch on the DrugPolicy / FoodPolicy / ReadingPolicy variants poisons the ApparelPolicy one.

Symptom: every frame, "Exception filling window for X: System.InvalidCastException: Specified cast is not valid" (or the `ArrayTypeMismatchException` above) with thousands of occurrences, deduped to a `[Ref XXXX]` line so you can't see the actual stack. Nothing fails at startup; it only fires when a window using a different T instantiation renders.

**Don't:** patch `Widgets.HorizontalSlider<T>`, `TabDrawer.DrawTabs<T>`, `Widgets.TextFieldNumeric<T>`, `Dialog_ManagePolicies<T>.DoPolicyListing`, `Window<T>.DoWindowContents`, or any other generic method or method of a generic class whose T is a reference type. (`Widgets.TextFieldNumeric<int/float/long>` has the same footgun but happens not to crash because real callers only use those exact types.)

**Do instead:** anchor on a non-generic method that the generic body calls, and scope the capture with a depth/nesting counter plus a window/dialog-open gate. For `DrawTabs`, patch `TabRecord.Draw` or `Widgets.ButtonInvisible`; for `DoPolicyListing`, patch `Widgets.BeginScrollView` / `Widgets.EndScrollView` (with `IsOpen` + width-based gating) to capture the outRect indirectly. Otherwise accept that you can't observe all instantiations and skip the patch.

`typeof(T).MakeByRefType()` (needed to target a `ref Vector2` parameter in `BeginScrollView`) is not a constant expression and fails CS0182 inside `[HarmonyPatch(..., new[] {...})]`; use `[HarmonyPatch]` with a `static MethodBase TargetMethod()` that constructs the by-ref type at runtime.

*Why it's tricky:* it compiles, the patch binds successfully via `PatchAll`, and closing the generic over one concrete type looks safe because the type is fully resolved at IL time. The crash only appears when *some other* part of the game uses a different T, with a stack trace pointing at vanilla code rather than your patch. The exception message names the window that was drawing (e.g. `Dialog_ManageApparelPolicies`), not the variant you patched. The Harmony docs do not warn about this.

## When patching an optional mod's methods, keep `typeof(OptionalMod.Type)` out of your main assembly's attributes

Use `[HarmonyPatch(typeof(X))]` only for types that are guaranteed to be loaded. For optional mods, the `typeof()` call bakes a CLR type token into the IL metadata that the runtime tries to resolve at class-load time — even when the optional mod is absent. This throws `System.TypeLoadException: Could not resolve type with token 010000xx` crashing the entire mod.

**Sparingly (a single third-party method):** apply the patch manually at startup using `AccessTools.TypeByName("OptionalMod.Type")` inside a guard (`if (!ModLister.HasActiveModWithName(...)) return;`). Use `object __instance` (not the optional type) in the patch method signature — a direct type reference there also causes the same TypeLoadException.

```csharp
public static void TryApplyHarmonyPatches(Harmony harmony)
{
    if (!ModLister.HasActiveModWithName("Optional Mod Name")) return;
    var t = AccessTools.TypeByName("OptionalMod.SomeClass");
    harmony.Patch(AccessTools.Method(t, "SomeMethod"),
        postfix: new HarmonyMethod(AccessTools.Method(typeof(MyPatches), nameof(Postfix))));
}
static void Postfix(object __instance) { /* use AccessTools.Field to read/write */ }
```

**Anything more involved:** give the integration its own assembly, loaded only when the other mod is active. Then the integration code references the other mod's types directly (normal `[HarmonyPatch(typeof(X))]`, full static typing, `Publicize` for its non-public members) and none of the reflection above is needed. See the sibling mod `RealisticOrbitalTrade` (`Source/RealisticOrbitalTrade.DynamicTradeInterface/` and its `LoadFolders.xml`) for a working setup:

- A separate csproj per integration, with `AssemblyName` set to the integration's name and `OutputPath` set to `../../Integrations/<Name>/$(RimWorldVersion)/Assemblies`.
- It has a `ProjectReference` to the main mod's project and a `Reference` to the other mod's DLL, both with `CopyLocal` False and `Private` false so neither is copied into the integration's output.
- `LoadFolders.xml` lists `Integrations/<Name>/<version>` per game version with `IfModActive="<other mod's packageId>"`, so the assembly never loads (and its type references never resolve) when the other mod is absent.

*Why it's tricky:* Harmony's `PatchAll` scans every type in the assembly and reads custom attributes. Reading `[HarmonyPatch(typeof(X))]` forces the CLR to resolve `X` immediately — before any runtime checks. This fails fatally if `X` lives in an unloaded optional mod's DLL. Putting the integration in a separate, conditionally-loaded assembly means that resolution only happens when the mod is present.

## When you need to filter what vanilla iterates over, use a Harmony Prefix with `ref IEnumerable<T>` reassignment

When a vanilla method takes `IEnumerable<T> input` (NOT `ref`) and you want to feed it a filtered subset without rewriting the whole method, declare the parameter as `ref IEnumerable<T>` in your Prefix and reassign it. Harmony writes the modified reference back to the original method's local, and the unmodified vanilla body then iterates your substitute.

```csharp
[HarmonyPatch(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid))]
static class Patch_FilterArchitectGizmos
{
    static void Prefix(ref IEnumerable<Gizmo> gizmos)
    {
        if (!ShouldFilter()) return;
        gizmos = gizmos.Where(g => ShouldKeep(g));
    }
}
```

Confirmed working on `GizmoGridDrawer.DrawGizmoGrid` for filtering the gizmo list. The original C# signature has `IEnumerable<Gizmo> gizmos` (no `ref`); Harmony still injects IL to read back from the local after the prefix returns.

*Why it's tricky:* you'd assume reassigning a reference-type parameter in a Prefix can't propagate (C# pass-by-value semantics). Harmony's IL injection makes `ref` on the Prefix side work even when the original is by-value. Without this trick, the only alternative is to Prefix-skip the whole vanilla method and reimplement it — which for `DrawGizmoGrid` means replicating ~300 lines of sort/group/wrap/draw logic plus accessing private statics like `gizmoGroups` and `firstGizmos`. The ref-substitute is 3 lines.

Caveats: only works when the vanilla code consumes the enumerable lazily AFTER the prefix runs. If vanilla snapshots to a List before the prefix's reassignment takes effect, this fails. Verify by testing — the failure mode is "filter has no effect", not a crash.

> **Unverified:** The `ref IEnumerable<T>` write-back behavior was not re-tested. Check this against the decompiler/vanilla defs before relying on it.

## HarmonyPatch attribute targets DeclaredMethod, not inherited

`[HarmonyPatch(typeof(T), nameof(T.M))]` uses `AccessTools.DeclaredMethod` under the hood, which passes `BindingFlags.DeclaredOnly`. If `M` is inherited from a base class and `T` doesn't override it, Harmony throws `Undefined target method for patch method ...` at `PatchAll` time and the whole `[StaticConstructorOnStartup]` HarmonyPatches type initializer fails — taking down every patch in the assembly with a `TypeInitializationException`.

Fix: patch the base type that actually declares the method, take `BaseType __instance`, and filter at runtime with `if (!(__instance is DerivedType)) return;`. (`nameof(Derived.Method)` still compiles for inherited members, which is why this bug hides past the compiler.)

*Why it's tricky:* The runtime `AccessTools.Method` (non-attribute path) DOES walk base types, so manual `harmony.Patch(AccessTools.Method(typeof(Derived), "M"), ...)` works fine — only the attribute form is hierarchy-blind. And the failure mode is catastrophic: a single mis-targeted patch class kills every other Harmony patch in the assembly because they all run from one static cctor.

## Postfix/Prefix parameter names must match the patched method by name

Harmony binds Prefix/Postfix parameters to the original method's parameters by **name**, not by position. If your patch declares `Postfix(Rect rect, ...)` but the original is `static bool ButtonInvisible(Rect butRect, bool doMouseoverSound)`, you get `System.Exception: Parameter "rect" not found in method static System.Boolean Verse.Widgets::ButtonInvisible(...)` at `PatchAll` time — which throws inside the `[StaticConstructorOnStartup] HarmonyPatches` cctor and kills every other patch in the assembly via `TypeInitializationException`.

Common RimWorld traps: `Widgets.ButtonInvisible(Rect butRect, ...)`, `Widgets.ButtonImage(Rect butRect, Texture2D tex, ...)`, `Widgets.BeginScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, ...)`. NOT named `rect`. `Widgets.ButtonText` IS `Rect rect` though, so it's per-method — always cross-check the actual vanilla signature.

Recipe: when patching anything, check the target method first, copy its parameter names verbatim into your Postfix signature. Don't guess. The C# compiler won't catch this — it's a runtime bind failure only.

## Widgets.RadioButton has NO (Rect, bool, bool) overload — patch ButtonInvisible instead

Vanilla `Widgets.RadioButton` only exposes `(Vector2, bool, bool)` and `(float, float, bool, bool)`. Both internally call `Widgets.ButtonInvisible(butRect)` with the 24×24 indicator rect. `Widgets.RadioButtonLabeled(Rect, ...)` also forwards to `ButtonInvisible(rect)`. So a single Postfix on `Widgets.ButtonInvisible` captures every radio-button click — do not try to `HarmonyPatch(typeof(Widgets), nameof(Widgets.RadioButton), new[] { typeof(Rect), typeof(bool), typeof(bool) })`, it'll throw at static-ctor time: `Undefined target method for patch method ...`.

*Why it's tricky:* The name "RadioButton" + a rect parameter sounds like it should exist (every other widget has an overload). It doesn't. Verify signatures before patching by listing the members of `Verse.Widgets` with the decompiler MCP server.

## When Harmony-patching a private method, publicize it so `nameof` works

`[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.MakeDowned))]` fails to compile with `CS0117: 'Pawn_HealthTracker' does not contain a definition for 'MakeDowned'` when the target method is private — `nameof` requires the symbol to be accessible from the caller. Add `<Publicize Include="Assembly-CSharp:Verse.Pawn_HealthTracker.MakeDowned" />` to the csproj and the attribute compiles. Harmony itself reaches private members via reflection at runtime either way.

*Why it's tricky:* the error message blames the type for not having the definition, not the visibility, so it looks like an API rename when it's just C# accessibility.

## HarmonyPatch on a virtual method works only if the named type actually overrides it

`[HarmonyPatch(typeof(Derived), nameof(Derived.SomeVirtual))]` requires `Derived` to actually declare/override `SomeVirtual`. If only the base declares it (and `Derived` just inherits), Harmony fails with `Undefined target method for patch method ... ::Postfix` and the static constructor of your patch class throws `TypeInitializationException` at startup — wrecks the whole mod load. Common RimWorld traps: `MainTabWindow_Work` does NOT override `PreOpen` or `RequestedTabSize` (both on `MainTabWindow_PawnTable` / `Window`); `MainTabWindow_Architect` DOES override `PreOpen` and `RequestedTabSize`, so it works.

Fix: patch the **declaring** type (`Window.PreOpen`, `MainTabWindow_PawnTable.RequestedTabSize`) and gate the Postfix on `__instance is YourTargetType`. The gate is one is-check per call; cheap.

*Why it's tricky:* "but MainTabWindow_Work has PreOpen at runtime — I can call it!" — yes, because it inherits. Inheritance ≠ declaration. Harmony reflects on the type literally, looking for a `MethodInfo` declared on that type. Inherited members aren't found.

> **Unverified:** The specific `MainTabWindow_Work` / `MainTabWindow_Architect` override claims were not checked. Check this against the decompiler/vanilla defs before relying on it.

## When patching frequently-called methods, gate the patch body on settings + early-return on common cases at the very top

A Harmony patch on a hot-path method (e.g. `Hediff.Tick`, `DamageWorker.Apply`, `Pawn.Tick`) runs FAR more often than the modder usually pictures. `Hediff.Tick` fires once per hediff per pawn per game tick — easily 5000+ calls/sec mid-game. If your patch body does anything substantive (allocates, calls reflection, dereferences nested optionals), the game stutters.

Recipe:
1. **First line of the patch body is a `if (!settings.featureEnabled) return;`** — a single bool read costs nothing.
2. **Second line is the type-narrowing guard** — `if (!(victim is Pawn p) || !p.IsColonistPlayerControlled) return;` etc. — so the bulk of calls (damage to plants, hediffs on raiders) short-circuit before any real work.
3. **Never** put a Harmony patch on `Hediff.Tick`/`Pawn.Tick`/`ThingWithComps.Tick` unless you've measured the cost. There's almost always a less-hot event you can hook instead — `Pawn_HealthTracker.NotifyPlayerOfKilled` (one fire per pawn death) beats `Hediff.Tick` for reacting to "pawn died" by orders of magnitude.

*Why it's tricky:* the patch itself looks tiny in source — a reflection call here, a property dereference there. None of those operations are "expensive" in isolation. The cost is the **call count**, which you only learn from a profile. Once a hot-path patch ships, players report inexplicable stutter that no Player.log error explains.

## Constraining where AI pawns can go: patch Reachability.CanReach, not job/target consumers

When you need to forbid a class of pawns from going to certain cells (e.g. only roofed cells), do NOT try to filter at the consumer level. RimWorld has dozens of AI subsystems that pick targets/destinations independently — `JobGiver_AIFightEnemy`, `TrashUtility.ShouldTrashBuilding`, `JobGiver_AIGotoNearestHostile`, `JobGiver_AISapper`, `JobGiver_AIDefendPoint`, lord-set duty.focus cells, breaching, infestations, escort, hunt, patrol, etc. Patching them one by one is whack-a-mole — there is always one more.

**Patch `Reachability.CanReach(IntVec3, LocalTargetInfo, PathEndMode, TraverseParms)` instead.** Every AI consumer ultimately funnels through it (or through the pathfinder, which calls it). Return `__result = false` for forbidden destinations and the entire AI stack naturally drops those targets from candidate sets. Vanilla AI runs untouched; you only narrow the reachable set.

```csharp
[HarmonyPatch(typeof(Reachability), nameof(Reachability.CanReach),
    new System.Type[] { typeof(IntVec3), typeof(LocalTargetInfo),
        typeof(PathEndMode), typeof(TraverseParms) })]
static class Patch {
    static bool Prefix(Reachability __instance, IntVec3 start,
                      LocalTargetInfo dest, TraverseParms traverseParams,
                      ref bool __result) {
        if (!ShouldConstrain) return true;
        Pawn pawn = traverseParams.pawn;
        if (pawn == null || pawn.IsColonist) return true; // exempt
        Map map = __instance.map; // needs <Publicize Include="Assembly-CSharp:Verse.Reachability.map" />
        if (map == null) return true;
        if (!IsForbiddenZone(map, dest.Cell)) return true;
        __result = false;
        return false;
    }
}
```

*Why it's tricky:* the obvious approach is to patch `AttackTargetFinder.BestAttackTarget` (which has a Predicate<Thing> validator slot, so it looks designed for this). It works for combat but misses building trash, lord focus, escort, etc. — each of those does its own target picking with its own validator. Reachability is the common denominator.

*Equally important:* do NOT patch `Pawn_PathFollower.StartPath` to deny outdoor paths — calling `EndCurrentJob` from inside it is reentrant and corrupts the pather state, freezing pawns. And do NOT patch `Pawn_JobTracker.StartJob` to filter jobs — vanilla AI emits dozens of intermediate Wait/Goto/idle jobs that you can't safely deny without breaking the think tree.

*Also:* if the constrained pawn type ends up in a custom infinite-loop "stay here" job (e.g. a SeekShelter wander loop), the `Pawn_JobTracker.CheckForJobOverride` only runs the constant think tree mid-job — `JobGiver_AIFightEnemy` lives in the main think tree. So the pawn will never start combat while in your job. End your custom job once the pawn satisfies your constraint, then vanilla AI's main think tree fires and combat works normally.

> **Unverified:** The claims that `Reachability.CanReach` is the common funnel for all AI target selection, and that patching `StartPath` / `StartJob` breaks the pather and think tree, were not checked. Check this against the decompiler/vanilla defs before relying on it.

## When patching RimWorld 1.6 ideology style methods, use these exact signatures

In RimWorld 1.6, the style API changed significantly from 1.5:

- `Pawn_StyleTracker.StyleCategory` **does not exist**. The property was removed entirely.
- `IdeoStyleTracker.StyleForThingDef` is the correct interception point for both pawn items (hair/beard/tattoos) and thing styles. Its exact signature is: `StyleCategoryPair StyleForThingDef(ThingDef thing, Precept precept)` — note the return type is `StyleCategoryPair` (not `ThingStyleDef`), and the parameter is named `thing` (not `thingDef`). Use a Postfix; `StyleCategoryPair` is a class with public `category` and `styleDef` fields, so assign them directly (null-check `__result` first).
- `CompStyleable` has a property `StyleCategoryDef` (not `StyleDef`). The backing field is `styleDef` (lowercase).
- `Faction.ideos` is a **field** (not a property like `ideoTracker`). Its type is `FactionIdeosTracker`, so use its members directly.
- `Ideo.style` is a field of type `IdeoStyleTracker` (not `StyleCategoryDef`).
- `StyleCategoryPair` fields: `StyleCategoryDef category` and `ThingStyleDef styleDef`, both public.

*Why it's tricky:* Between 1.5 and 1.6 Ludeon restructured the ideology style system, renaming and removing members. `PatchAll` throws `HarmonyException: Patching exception in method null` when any `[HarmonyPatch]` attribute targets a non-existent property getter — crashing the entire mod constructor silently from RimWorld's perspective.

## When a Postfix mutates ref __result, handle the null case

If the patched method returns a reference type, `__result` can be null. Check `if (__result == null) __result = new StyleCategoryPair();` before assigning its fields. This bites especially when patching ideology lookup methods like `IdeoStyleTracker.StyleForThingDef`, which return null for any ThingDef the ideology doesn't have a style entry for — extremely common during pawn generation.

## In RimWorld 1.5+, hide a pawn's rendering by patching DynamicDrawPhaseAt, not DrawAt

A Harmony prefix on `Pawn.DrawAt` returning `false` no longer hides the pawn in 1.5+. Pawn rendering goes through `Pawn.DynamicDrawPhaseAt(DrawPhase, Vector3, bool)`, which dispatches the three phases (PreDraw / ParallelPreDraw / Draw). Patch this instead:

```csharp
[HarmonyPatch(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt))]
public static class Patch { public static bool Prefix(Pawn __instance) => !ShouldHide(__instance); }
```

Unlike `DrawAt`, `DynamicDrawPhaseAt` is public so `nameof` works directly (no string overload needed).

*Why it's tricky:* compile + Harmony patch both succeed silently with `DrawAt`; the pawn just keeps rendering because the new pipeline doesn't go through it. The clue is in the decompiled `Pawn` and `PawnRenderer` types: search them with the decompiler MCP for `DynamicDrawPhaseAt`, `RenderPawnInternal` and `ParallelPreRenderPawnAt` — the pre-1.5 `DrawAt` path is essentially vestigial for pawns.

## 1.4 → 1.6 ritual API renames: OutcomeChance → RitualOutcomePossibility, ExpectedOutcomeDesc → QualityFactor

When porting Ideology ritual code from 1.4 to 1.5/1.6, four overrides shifted:

- `RitualOutcomeEffectWorker_FromQuality.ApplyExtraOutcome(..., OutcomeChance outcome, ...)` → `..., RitualOutcomePossibility outcome, ...`. Same shape, just renamed.
- `RitualOutcomeComp_Quality.GetExpectedOutcomeDesc(...)` returning `ExpectedOutcomeDesc` → `RitualOutcomeComp.GetQualityFactor(...)` returning `QualityFactor`. The field `effect` on the return value was renamed to `qualityChange`; other fields (label/count/quality/positive/priority) carry over.

*Why it's tricky:* Harmony patches against `ApplyExtraOutcome` by name still work because Harmony resolves the method by `MethodInfo` after the type change, but **subclass overrides** silently no-op (CS0115: no suitable method found to override) — the only signal is a build error pointing at the old return type. Both renames are pure renames with no logic change, so the fix is mechanical once you know what to search for.
