## Right-click on map with no selection opens the Architect menu

A right-click on the map with nothing selected opens the Architect menu. This is a vanilla convenience shortcut, but it also fires for right-click events your mod generates (custom input handling, scripted input). The shortcut lives in `RimWorld.MainTabsRoot.HandleLowPriorityShortcuts`:

```csharp
if (Find.Selector.NumSelected == 0 && Event.current.type == EventType.MouseDown && Event.current.button == 1 && !WorldRendererUtility.WorldSelected && ...)
{
    Event.current.Use();
    MainButtonDefOf.Architect.Worker.InterfaceTryActivate();
}
```

Fix: Harmony-prefix `MainTabsRoot.HandleLowPriorityShortcuts` and call `Event.current.Use()` when your own right-click just fired. Map right-clicks (move orders, context menus) run earlier in `UIRoot_Play.UIRootOnGUI` via `mapUI.HandleMapClicks()`, so consuming the event in this later prefix doesn't break them.

*Why it's tricky:* The vanilla code is an *intentional* convenience shortcut and looks innocuous in isolation — you'd never grep for "architect" while debugging a stray right-click. The clue is `MainTabsRoot.HandleLowPriorityShortcuts` running near the tail of OnGUI, after map clicks.

## RimWorld 1.6 API drift cheat sheet for porting from 1.5

Common 1.5→1.6 renames that produce CS0115/CS0117/CS1061 in C# mods being ported:

- `WorldComponent.FinalizeInit()` → `FinalizeInit(bool fromLoad)`. Override must take the bool, and call `base.FinalizeInit(fromLoad)`.
- `PawnLostCondition` (in `Verse.AI.Group`): `IncappedOrKilled` removed — use `PawnLostCondition.Incapped` (it's now split from `Killed`, check both if you need both).
- `PawnLostCondition.LeftMap` renamed to `ExitedMap`.
- `Pawn_StoryTracker.adulthood` → use the `Adulthood` property. Same with `Childhood`.
- `GenLocalDate.Quadrum(...)` removed — use `GenDate.Quadrum(GenTicks.TicksAbs, 0f)` (or a real longitude). `GenLocalDate` still has `DayOfQuadrum`, `Twelfth`, `Season`, etc.
- `Pawn_HealthTracker.MakeDowned` is private — publicize it to use `nameof` in the `[HarmonyPatch]` (see harmony topic).

*Why it's tricky:* most of these surface as "no definition" or "no override" errors that look like the type is missing, when really it's a signature/visibility change. Look up the new shape with the decompiler MCP server rather than trusting old tutorials.

## LookTargets in RimWorld is a CLASS not a struct — default(LookTargets) is null, optional params need null-check

`Verse.LookTargets` is `public class LookTargets : IExposable` (Verse/LookTargets.cs), NOT a struct. This means `default(LookTargets)` is `null` and any method that accepts `LookTargets lookTargets = default` as an optional parameter will receive `null` when callers omit it. Touching `.targets` without a null check NPEs with "Object reference not set to an instance of an object."

Recipe: any helper that takes `LookTargets` as an optional/default parameter must null-check the wrapper FIRST before touching its `.targets` field. Pattern: `bool hasTargets = lookTargets != null && lookTargets.targets != null && lookTargets.targets.Count > 0;`. If you want a non-null sentinel, callers must explicitly pass `LookTargets.Invalid` (a real instance with an empty/special targets list).

*Why it's tricky:* RimWorld has several Verse types that feel struct-like by name and usage (`GlobalTargetInfo`, `TargetInfo`, `LocalTargetInfo` — all real structs; `LookTargets` — class). When you write a letter-dispatching helper inspired by `Find.LetterStack.ReceiveLetter(label, body, def, lookTargets, faction)`, it's natural to assume the parameter is uninitialised-safe like a struct. It isn't. The default-parameter convention you'd use for a struct produces a null reference at runtime.

## default(CellRect) is NOT empty — it's a 1×1 rect at (0,0) with Area==1

In RimWorld, `CellRect.Width => (minX > maxX) ? 0 : maxX - minX + 1`. For `default(CellRect)` all fields are 0, so minX==maxX==0 → Width = 0-0+1 = **1**, Height = 1, and **Area == 1** — a 1×1 cell at the map corner (0,0,0). So `return default;` as a "no rect found" sentinel is a trap: every `rect.Area > 0` validity check reads it as a real room at (0,0), and `rect.CenterCell` is (0,0,0).

Example failure: a placement helper returns `default` when no slot fits; the caller sees Area==1 > 0, treats it as a real rect at (0,0,0), and builds toward the map corner.

Fix: use an explicit Area-0 sentinel `new CellRect(0, 0, 0, 0)` — the 4-arg constructor sets `maxX = minX + width - 1 = -1`, so minX(0) > maxX(-1) → Width=0 → Area=0, which `Area > 0` correctly rejects. Never use `default(CellRect)` to mean "none". Same caution applies to any struct field left at its default value and later gated on `.Area`.

## When you want a second camera/viewport to render the RimWorld map, widen CurrentViewRect (scoped) — the renderer is single-camera and view-rect-culled

RimWorld's map renderer is built around ONE camera. A second Unity `Camera` pointed at the map (e.g. a split-screen viewport) renders BLACK everywhere the main camera isn't looking — and shows only a tiny patch when the main camera is zoomed in.

Why: the per-frame draw in `Map.MapUpdate()` runs only for `Find.CurrentMap`, and both geometry passes cull to the single main camera's view rect:
- Static mesh: `MapDrawer.DrawMapMesh` → `ViewRect => Find.CameraDriver.CurrentViewRect.ExpandedBy(1).ClipInsideMap(map)`.
- Dynamic things (pawns/items/filth): `DynamicDrawManager.DrawDynamicThings` → `ComputeCulledThings` → `Find.CameraDriver.CurrentViewRect`.
The draws themselves are cameraless `Graphics.DrawMesh(mesh, matrix, mat, layer)`, so ANY active camera renders whatever IS enqueued — the problem is purely that only the main camera's view rect gets enqueued.

Fix for "same map, independent zoom/pan in a 2nd viewport": while those two render methods run, widen the reported view rect to the UNION of both cameras' view rects (`leftRect.Encapsulate(rightRect)`). Each camera's viewport `rect` then clips the extra geometry to its own half. Recipe: a postfix on the `CameraDriver.CurrentViewRect` getter (`ref CellRect __result`) that encapsulates the second rect, gated by a flag that prefixes/postfixes on `MapDrawer.DrawMapMesh` and `DynamicDrawManager.DrawDynamicThings` set/clear.

*Why it's tricky:* `CurrentViewRect` is read by ~25 call sites, several of them GAMEPLAY/AI (InfestationCellFinder, UnnaturalCorpse escape logic, Pawn_CallTracker, RegionGrid). You MUST scope the widening to only the render passes via the flag — widening it globally silently changes AI/spawn behavior. Compute the second viewport's rect by mirroring `CurrentViewRect`'s own math but with that viewport's aspect (half-width → `UI.screenWidth*0.5/UI.screenHeight`) and the second camera's position/orthographicSize. Different MAPS per viewport is a much harder problem (MapUpdate only draws `Find.CurrentMap`, and cameraless draws bleed into both viewports) — needs camera-routed `Graphics.DrawMesh(...,Camera)` or RenderTextures.

> **Unverified:** The view-rect call sites and the claim about ~25 `CurrentViewRect` readers were not checked. Check this against the decompiler/vanilla defs before relying on it.
