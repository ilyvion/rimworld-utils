## `CS1061` / `CS0246` from a missing `using` — RimWorld splits APIs across sub-namespaces

When a method or type "should exist" but the compiler insists it doesn't, the type is usually in a sub-namespace that the global usings (`RimWorld`, `Verse`, `UnityEngine`, `HarmonyLib`) don't pull in. The pattern is `error CS1061: '<Type>' does not contain a definition for '<Member>' and no accessible extension method` (extension methods) or `CS0246: The type or namespace name '<Name>' could not be found` (plain types). Add the right `using` instead of fishing in the existing namespaces.

Most-bitten cases:

| Symbol | Lives in | Notes |
| --- | --- | --- |
| `Pawn.IsWorldPawn()`, `Find.WorldPawns`, `Caravan`, `Settlement`, `WorldObject`, `Tile` | `RimWorld.Planet` | Anything off the planet/world map. |
| `QuestNode`, `QuestNode_*`, `QuestGen`, `Slate` | `RimWorld.QuestGen` | Quest scripting. `QuestPart` and `QuestPart_*` are in `RimWorld`, not `QuestGen`. |
| `JobDriver`, `Toil`, `Toils_*`, `ThinkNode`, `MentalState_*`, `Pawn_PathFollower` | `Verse.AI` | The core pawn AI / job execution types. Easy to miss because `JobDef` itself is in `Verse`. Many concrete subclasses (`JobDriver_Ingest`, `WorkGiver`, `WorkGiver_*`) are in `RimWorld`. |
| `SoundStarter` (the `PlayOneShot*` / `TrySpawnSustainer` extensions), `Sustainer`, `SoundInfo` | `Verse.Sound` | `SoundDef` is in `Verse` and `SoundDefOf` is in `RimWorld`. |
| `SketchResolver` | `RimWorld.SketchGen` | Procedural building generation. `Sketch`, `SketchEntity` and `SketchResolverDef` are in `RimWorld`. |
| `SymbolResolver`, `BaseGenUtility` | `RimWorld.BaseGen` | Base/raid scenario generation. |

When in doubt, look the symbol up with the decompiler MCP server (`search_types` / `search_symbols`) and read the `namespace` of the result.

*Why it's tricky:* RimWorld's namespace split is not aligned with what the type does. `JobDef` is in `Verse` but everything that *uses* it is in `Verse.AI`. `WorldPawnsUtility` (which defines the `IsWorldPawn` extension) is in `RimWorld.Planet` even though the extended type (`Pawn`) is in `Verse`. Importing `Verse` and `RimWorld` does not transitively import their child namespaces.

## MapComponent.FinalizeInit runs on a background thread during map generation — do NOT call Unity graphics APIs there

`MapComponent.FinalizeInit()` is called from `Map.FinalizeInit()` which runs inside `LongEventHandler.RunEventFromAnotherThread` during map generation. This means it executes on a background thread, NOT the Unity main thread.

**Any Unity graphics API call from FinalizeInit will cause a native crash:** `new Texture2D(...)`, `new Material(...)`, `Shader.Find(...)`, `GameObject.AddComponent(...)`, etc. The crash signature is `Texture2D:Internal_CreateImpl` in the stack trace with `LongEventHandler:RunEventFromAnotherThread` further up.

**Fix:** defer all Unity API calls to `MapComponentUpdate()` or `MapComponentDraw()` (both run on the main thread). Use a `bool graphicsInitialized` flag for lazy one-shot init:

```csharp
public override void FinalizeInit()
{
    // Pure data setup only — safe on any thread
    field = new MyDataStructure();
}

public override void MapComponentUpdate()
{
    if (!graphicsInitialized)
        TryInitGraphics(); // Texture2D, Material, Shader.Find here
    // ...
}
```

*Why it's tricky:* the crash is a native Unity crash (`Got a UNKNOWN while executing native code`), not a clean C# exception. The stack trace shows `Mono JIT Code` mixed with `UnityPlayer` native frames. Easy to misattribute to GPU drivers or Unity bugs when it's actually a threading violation.

## Mod constructor runs on a background thread via LongEventHandler — do NOT call Unity graphics APIs (Shader.Find, new Material, Texture2D, etc.) there

The Mod constructor (`Mod(ModContentPack)`) is called from `LoadedModManager.CreateModClasses()` which runs inside `PlayDataLoader.DoPlayLoad()` → `LongEventHandler.RunEventFromAnotherThread`. This means the Mod constructor executes on a background thread, NOT the Unity main thread.

Any Unity graphics API call from the constructor will cause a native Unity crash:
- `Shader.Find(...)` → crash in `ResourcesAPIInternal:FindShaderByName`
- `new Material(...)`
- `new Texture2D(...)`
- `GameObject.AddComponent(...)`

The crash stack trace signature: `LongEventHandler:RunEventFromAnotherThread → PlayDataLoader:DoPlayLoad → LoadedModManager:CreateModClasses → Mod.ctor` with a native crash in `UnityEngine.ResourcesAPIInternal:FindShaderByName` or similar.

Fix: defer all Unity API calls to the first render frame (`Camera.onPostRender`), `MapComponentUpdate()`, or `MapComponentDraw()` — all of which run on the main thread. Use a lazy-init flag pattern.

*Why it's tricky:* it's a native Unity crash, not a managed exception. The managed stack trace shows `Mono JIT Code` mixed with `UnityPlayer` native frames. Nothing in the log says "called off main thread" — you have to spot `RunEventFromAnotherThread` in the trace.

## Finding the real name of a RimWorld type

Type names rarely match what you'd guess (`WeatherOverlay_Rain` lives in `RimWorld`, not `Verse`). Search the assembly with the decompiler MCP server (`search_types`) rather than guessing a name.

## 1.4 → 1.6 tick-batched job system: GainComfortFromCellIfPossible / JoyTickCheckEnd now require a delta

RimWorld 1.6 batches job ticks to amortize cost. APIs that used to be called every tick now take an `int delta` parameter (how many ticks to advance):

- `PawnUtility.GainComfortFromCellIfPossible(this Pawn p, int delta, bool chairsOnly = false)`
- `JoyUtility.JoyTickCheckEnd(Pawn pawn, int delta, JoyTickFullJoyAction = EndJob, float = 1f, Building = null)`
- (Same pattern on most `*Tick*` helpers — if a 1.4-era call breaks with CS7036 "delta required", that's why.)

In a `Toil.AddPreTickAction` (runs every tick), pass `delta=1`. In a toil that uses `tickIntervalAction` or its own batching, pass the matching interval.

*Why it's tricky:* the second arg to `JoyTickCheckEnd` used to be `JoyTickFullJoyAction`; in 1.6 that's argument 3. A literal `JoyTickFullJoyAction.None` in arg 2 surfaces as CS1503 ("cannot convert from JoyTickFullJoyAction to int") rather than as a count mismatch, which is the giveaway.

## Bare ampersand (&) in About.xml description breaks entire mod loading with cryptic EntityName parse error

In `About.xml`, the `<description>` field is raw XML text, not CDATA. If you write `[b]Compatibility & Resilience Pack[/b]`, the bare `&` triggers `System.Xml.XmlException: An error occurred while parsing EntityName` because XML treats `&` as the start of an entity reference. The mod fails to load entirely — no defs parsed, no assemblies loaded, button doesn't appear — and the only clue is a vague log line: `Exception loading file at ...\About\About.xml. Loading defaults instead.`

**Always escape `&` as `&amp;` in About.xml descriptions.** Other characters like `<` and `>` must also be escaped as `&lt;` and `&gt;` unless wrapped in a CDATA block. This is a silent mod-killer that gives no in-game error message.

*Why it's tricky:* the `About.xml` parser runs before any defs or code, so the entire mod is invisible. The log message says "Loading defaults instead" which makes it look like a minor issue, but it means the mod is completely dead.
