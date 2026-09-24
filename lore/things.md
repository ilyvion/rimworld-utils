## Plant `<preferability>RawBad</preferability>` requires Nutrition > 0 — use `NeverForNutrition` for inedible plants

`Config error in Plant_X: Nutrition == 0 but preferability is RawBad instead of NeverForNutrition`. RimWorld validates that any plant whose nutrition is zero must declare `<preferability>NeverForNutrition</preferability>`. `RawBad` is reserved for plants that *are* edible but unappealing. Either set a non-zero `<statBases><Nutrition>...</Nutrition></statBases>` or downgrade preferability.

*Why it's tricky:* `RawBad` reads like "barely food at all" and feels right for a decorative plant — the engine treats it as a stricter contract than the English suggests. The error only fires at game load, never at mod-build time.

## When placing a blueprint programmatically, only pass stuff if def.MadeFromStuff is true

`GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, faction, stuff)` accepts a stuff parameter, but passing a non-null `stuff` to a def that isn't `MadeFromStuff` (e.g. `ThingDefOf.Campfire`, most workbenches that bake their material into the def) makes RimWorld emit two errors and null the stuff internally:

```
Got AdjustedCostList for Campfire with stuff WoodLog but is not MadeFromStuff.
MakeThing error: Frame_Campfire is not madeFromStuff but stuff=WoodLog. Setting to null.
```

The blueprint still places, but the log is dirty. Normalize stuff at the helper boundary:

```csharp
if (!def.MadeFromStuff) stuff = null;
else if (stuff == null) stuff = GenStuff.DefaultStuffFor(def);
```

*Why it's tricky:* the obvious tutorial example is `ThingDefOf.Wall` + `ThingDefOf.WoodLog`, which works because Wall IS MadeFromStuff. A generic helper that just always passes a default stuff (WoodLog) for "wooden things" blows up the moment it hits Campfire / TorchLamp / FueledStove / Hopper etc. The error attribution lands on `[RimWorld]` (because the bad cast happens inside CostListCalculator), not on your mod — easy to miss who caused it.

## Zone.AddCell rejects cells where any thing has def.CanOverlapZones=false — pre-filter the cell's ThingList

`Zone.AddCell(IntVec3)` walks `Map.thingGrid.ThingsListAt(c)` and errors out (`Log.Error("Added zone over zone-incompatible thing " + thing)`) if any thing in the cell has `def.CanOverlapZones == false`. That includes Blueprints, Frames, Buildings, and most Plants — so checking just `map.zoneManager.ZoneAt(c) == null` and `c.GetEdifice(map) == null` is NOT enough; blueprints and frames are NOT edifices.

Recipe — replicate the engine's predicate exactly before calling AddCell:

```csharp
var things = c.GetThingList(map);
for (int i = 0; i < things.Count; i++)
    if (!things[i].def.CanOverlapZones) return false;
```

*Why it's tricky:* the obvious filters (`Standable`, `ZoneAt(c) == null`, `GetEdifice(map) == null`) cover the visible cases — finished buildings and existing zones. Blueprints look like zones in the sense that they're transient overlays, so it's tempting to assume the engine handles them. It doesn't — AddCell logs an error AND returns without adding the cell, so your zone silently ends up with fewer cells than you placed. Attribution lands on `[RimWorld]` plus your mod because the call stack passes through your AddCell loop.

## Zero-cost blueprints get a Frame via PlaceNoCostFrame; a Frame with WorkToBuild=0 causes an endless "Construction botched" loop for low Construction skill

When a `Blueprint` has `TotalMaterialCost().Count == 0` (no costList AND not MadeFromStuff), `WorkGiver_ConstructDeliverResourcesToBlueprints.NoCostFrameMakeJobFor` issues `JobDefOf.PlaceNoCostFrame` instead of the material-delivery job. `JobDriver_PlaceNoCostFrame` calls `Toils_Construct.MakeSolidThingFromBlueprintIfNecessary`, which turns the blueprint into a `Frame` straight away (no materials to deliver); the frame is then built by `JobDriver_ConstructFinishFrame` like any other.

`JobDriver_ConstructFinishFrame`'s tick action does `Rand.Value < 1f - Mathf.Pow(statValue, num / workToBuild)` for player-faction builders, where `statValue` is `ConstructSuccessChance` (below 1.0 for low Construction skill) and `num` is the work done this tick. For `workToBuild == 0`, `num / 0 == +Infinity` and `Mathf.Pow(statValue < 1, +Infinity) == 0`, so the check is `Rand.Value < 1` — always true. The skill check runs BEFORE `workDone` is incremented, so the frame never completes: `FailConstruction` destroys it, the blueprint respawns, the WorkGiver re-issues `PlaceNoCostFrame`, and the "Construction botched" floating text repeats forever.

Vanilla players don't hit this because they place free buildings with a skilled constructor (`statValue` exactly 1.0 gives `Pow(1, +Inf) == 1`, so the check is never true). Any mod that puts low-skill pawns on 0-WorkToBuild blueprints WILL hit it.

Fix — Harmony prefix `Frame.FailConstruction` and convert it into `CompleteConstruction` when `WorkToBuild <= 0`:

```csharp
[HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
public static class Patch_Frame_FailConstruction_ZeroWork {
    public static bool Prefix(Frame __instance, Pawn worker) {
        if (__instance == null || __instance.WorkToBuild > 0f) return true;
        __instance.CompleteConstruction(worker);
        return false;
    }
}
```

*Why it's tricky:* the failure path is a `Mathf.Pow` + `Rand.Value` + comparison buried in an anonymous delegate inside `MakeNewToils`, hard to transpile and hard to find by searching. The "Construction botched" mote is a normal in-game mote that isn't attributed to any mod and isn't an exception, so nothing in the log points at it.

> **Unverified:** The `FailConstruction` loop was derived from the decompiled tick action but not reproduced in game, and I could not find `SleepingSpot` in the Core defs to confirm its `WorkToBuild` is 0. Check this against the decompiler/vanilla defs before relying on it.

## When you want stacking hediffs that escalate label + bonuses, bump severity on existing instead of AddHediff every time

For "kill counter" / "rank progression" hediffs where each event should make the hediff stronger AND change its label, do not call `pawn.health.AddHediff(MakeHediff(def))` every time — RimWorld's HediffSet may merge or duplicate awkwardly. Instead:

```csharp
var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
if (existing != null) existing.Severity = Mathf.Min(def.maxSeverity, existing.Severity + 1f);
else { var h = HediffMaker.MakeHediff(def, pawn); h.Severity = 1f; pawn.health.AddHediff(h); }
```

Then in the HediffDef XML, define multiple `<stages>` with `<minSeverity>` thresholds (e.g. 0, 3, 7) and per-stage `<label>` overrides — RimWorld auto-picks the right stage label based on current severity. Result: one entry on the health tab whose label transitions ("slayer" → "sworn slayer" → "legendary slayer") and whose statOffsets scale with kill count.

*Why it's tricky:* with multiple AddHediff calls, you either get N duplicate entries (visual clutter) or RimWorld silently swallows duplicates depending on the hediff class — neither is the "promotion" feel you want. The bump-severity pattern matches how vanilla addiction / drug-tolerance hediffs work.

> **Unverified:** The claim that AddHediff duplicates or merges depending on hediff class, and the comparison to vanilla addiction hediffs, were not checked. Check this against the decompiler/vanilla defs before relying on it.

## Don't mutate a collection you don't own — RimWorld's getters often return internal lists (e.g. listerThings.ThingsOfDef / ThingsInGroup)

Assume any list, dictionary or other collection you receive from a property or method is a **reference to someone else's state**, not a fresh copy, unless you've verified otherwise. Only mutate a collection when you know you own it or when mutating it is the intent (a documented API for that purpose); otherwise copy it first.

`map.listerThings.ThingsOfDef(def)` and `map.listerThings.ThingsInGroup(group)` are a concrete case: they return **references to the internal lists**. Calling `.Add()`, `.Remove()`, or any other mutation on them **corrupts the group membership system**, causing InvalidCastExceptions when code later tries to cast items from a group to a specific type (e.g., casting a Blueprint as a Corpse because it was wrongly added to the Corpses group).

**Fix**: Iterate read-only, or make a shallow copy if you need to collect or mutate results: `var copy = new List<Thing>(map.listerThings.ThingsOfDef(def));` then mutate the copy. To check who owns a collection, read the getter with the decompiler MCP (`get_decompiled_source`): a getter that returns a stored field hands out the internal instance.

*Why it's tricky:* nothing in the signature signals ownership: a method that returns a list looks the same whether it builds a new one or returns its own field. The corruption is silent until a cast fails deep in pathfinding or alert code, and the error is then attributed to the wrong system. The same applies when iterating: mutating the source collection while enumerating it throws, or worse, skips elements.

## For a custom NPC world-map mover with smooth motion + pawn roster + save/load, subclass Caravan not WorldObject

When you need a custom NPC entity that walks tile-to-tile on the world map and carries a pawn roster (a war band, a mercenary company, etc.), **subclass `RimWorld.Planet.Caravan`** and author your own `WorldObjectDef` with `<worldObjectClass>YourSubclass</worldObjectClass>`. Spawning pattern (mirror `CaravanMaker.MakeCaravan` but with your def):

```csharp
var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("YourArmy");
var army = (YourArmy)WorldObjectMaker.MakeWorldObject(def);
army.Tile = startTile;
army.SetFaction(faction);
Find.WorldObjects.Add(army);
foreach (var p in pawns) {
    army.AddPawn(p, addCarriedPawnToWorldPawnsIfAny: true);
    if (!p.IsWorldPawn()) Find.WorldPawns.PassToWorld(p);
}
army.SetUniqueId(Find.UniqueIDsManager.GetNextCaravanID());
army.pather.StartPath(targetTile, new YourArrivalAction(), repathImmediately: true);
```

You inherit: `Caravan_Tweener.TweenedPos` for free smooth interpolation (just override `DrawPos` to add a sine bob if you want); the pawn-cluster icon render; faction colouring; the selection inspector tabs (gear/health/social); save/load of every pawn and the pather state.

*Why it's tricky:* the temptation is to subclass `WorldObject` directly to get full control, but you then have to reimplement smooth motion, the pawn cluster, the inspect tabs, etc. Caravan already has all of that built; the only thing you give up is being able to disable inventory/needs handling, and for an NPC mover that doesn't matter — non-player caravans coexist with the needs system fine.

## Floating/bobbing custom Things must offset Z (screen-vertical), not Y — Y bob flickers via sort-order swaps

RimWorld's gameplay camera looks straight down +Y, so **Y is sort order, not screen-vertical**. If you bob a custom Thing's draw position by ±N on Y, you'll sweep through several `AltitudeLayer` slots per second; with `shaderType=Transparent` (or even Cutout against transparent neighbors) the sprite flips between drawing in front of and behind nearby pawns/motes every frame — the visible symptom is **flicker**, not floating. Screen-vertical motion goes on **Z**.

Recipe for a floating decorative Thing:
- Override `DrawAt`, not `DrawPos`. `DrawPos` is read by labels, selection brackets, culling, holders, etc.; offsetting it leaks into all of them.
- Put rise/bob on `drawLoc.z`. Leave `drawLoc.y` alone so sort order is stable.
- Wobble on `drawLoc.x` if you want a little sway.
- Pick `altitudeLayer` to match where the thing should sort (e.g. `MoteOverhead` for floating debris above pawns), then never modify y at runtime.
- Avoid `category=Item` for visual-only things — `Thing.TrueCenter` for Items routes through `ItemCenterAt`, which scans the cell's thing list and switches to a multi-item layout if anything else briefly shares the cell. Use `Projectile` or `Ethereal` instead.

```csharp
protected override void DrawAt(Vector3 drawLoc, bool flip = false)
{
    float lift = 0.6f + Mathf.Sin(bobPhase) * 0.18f;
    float drift = Mathf.Sin(bobPhase * 0.7f) * 0.12f;
    Vector3 loc = new Vector3(drawLoc.x + drift, drawLoc.y, drawLoc.z + lift);
    Graphic.Draw(loc, flip ? Rotation.Opposite : Rotation, this);
}
```

*Why it's tricky:* In 3D-engine intuition, "Y is up". RimWorld's `def.Altitude`, `AltitudeLayer`, and `ToVector3ShiftedWithAltitude` all bake into Y — which reinforces the wrong mental model. The Y-bob looks correct in code until you actually play it and see the flicker; the cause (depth-sort flipping the rock past Pawn/MoteOverhead/Blueprint layers) is invisible in source. `Skyfaller.GetDrawPositionAndRotation` is the in-engine confirmation: skyfallers fall via `zPositionCurve`, not Y.

> **Unverified:** The Y-sort flicker explanation, the `Item` category `ItemCenterAt` behavior and the `Skyfaller` reference were not checked. Check this against the decompiler/vanilla defs before relying on it.

## TryAttachFire silently no-ops on non-pawns; use FireUtility.TryStartFireIn instead

`Thing.TryAttachFire(fireSize, instigator)` calls `CanEverAttachFire()` which **requires `ThingCategory.Pawn`**. On any non-pawn (item, plant, building) it silently returns void with no log message — your code looks fine, the item just never catches fire.

To set fire to an item or plant, spawn a *free-standing* Fire on its cell:
```csharp
FireUtility.TryStartFireIn(item.Position, item.Map, fireSize, instigator);
```
The Fire's vanilla `DoComplexCalcs` then iterates `ThingsListAt(Position)` and damages every flammable Thing on the cell. `TryStartFireIn` is idempotent — it returns 0 chance if a Fire is already on the cell, so calling it every tick is safe.

*Why it's tricky:* The extension method name and signature look generic ("attach fire to any Thing"), and the silent-failure makes it look like the item is just "fire-resistant". The category check is buried inside `CanEverAttachFire`, which is itself called only from inside `TryAttachFire`. Easy to miss without reading the source.

## WorldObject uses ID not loadID; PlanetTile.tileId not .TileID

When subclassing `WorldObject` (e.g. a custom world-map entity), the unique-id field is **`public int ID = -1`**, not `loadID`. Key any dictionaries by `obj.ID`. `WorldObjectMaker.MakeWorldObject` sets `ID` from `Find.UniqueIDsManager.GetNextWorldObjectID()` — don't overwrite it.

`PlanetTile` is a readonly struct. Its tile index is **`public readonly int tileId`** (lowercase). `PlanetTile.TileID` does not exist. To log it: `tile.tileId`. The struct has an implicit `operator int` so `(int)tile` also works. The `Valid` property is `tile.tileId >= 0`.

*Why it's tricky:* `loadID` is the ID field name on some other types, so `obj.loadID` feels natural but is a compile error on `WorldObject`. In 1.6 a world tile is a `PlanetTile` struct rather than a plain int, so old examples that treat `settlement.Tile` as an int no longer apply.

## ThingComp.CompTickRare/CompTick won't fire unless the ThingDef sets tickerType

If your building has comps that override `CompTickRare()` or `CompTick()` and they appear to never run, the ThingDef is almost certainly missing `<tickerType>Rare</tickerType>` (or `Normal`). Default is `Never`, which means the Thing is never added to a tick list and none of its comps tick — silently. Symptoms: comp logs nothing, scans/timers don't progress, no errors.

```xml
<ThingDef ParentName="BuildingBase">
  ...
  <tickerType>Rare</tickerType>  <!-- required for CompTickRare to fire -->
  ...
</ThingDef>
```

*Why it's tricky:* there's no error or warning. The comp class loads, `PostSpawnSetup` runs, draw works — everything looks fine except the tick body. `CompPowerTrader`/`CompFlickable` happen to work because their behaviors don't depend on tick callbacks you control.

## Filth placementMask vs TerrainDef.filthAcceptanceMask — natural ground rejects Terrain-only filth

If `FilthMaker.TryMakeFilth` silently returns false on natural terrain (Sand, Soil, Grass, etc.), the issue is the mask interaction:

- Filth's `<filth><placementMask>` lists what KIND of cell it can sit on (e.g. `Terrain`, `Unnatural`).
- TerrainDef's `<filthAcceptanceMask>` lists what masks the terrain will accept. Natural terrains have `<li>Unnatural</li>`.
- A filth needs its placementMask to intersect the terrain's filthAcceptanceMask, or the placement is rejected with no error.

`<li>Terrain</li>` is for filth that sits on **built/artificial** floors. Vanilla `Filth_Dirt` is `Terrain`-only — that's why dirt only appears tracked indoors.

Two options for "I want this filth on any cell":
1. Omit `<placementMask>` entirely (what `Filth_Blood` does — works everywhere).
2. List both: `<placementMask><li>Terrain</li><li>Unnatural</li></placementMask>`.

*Why it's tricky:* `FilthMaker.TryMakeFilth` returns a bool but no diagnostic. The only way to find this is to log the rejected calls and notice they all happen on natural ground. The English word "placementMask" sounds like "place it on terrain", but `Terrain` here means *artificial terrain*, not *the ground at all*.

> **Unverified:** The mask semantics, the `Filth_Dirt` / `Filth_Blood` examples and the natural-terrain `filthAcceptanceMask` values were not checked. Check this against the decompiler/vanilla defs before relying on it.

## weaponClasses entries must be one of 10 vanilla WeaponClassDefs — `Medieval` is NOT one

`<weaponClasses><li>X</li></weaponClasses>` on a ThingDef takes a `WeaponClassDef` defName, not a free-form tag. The vanilla set is fixed at 10: `Melee`, `MeleeBlunt`, `MeleePiercer`, `Neolithic`, `Ranged`, `LongShots`, `ShortShots`, `RangedLight`, `RangedHeavy`, `Ultratech`. Anything else throws `Could not resolve cross-reference to WeaponClassDef named <X> (wanter=weaponClasses)` at game start.

Specifically: there is NO `Medieval` WeaponClassDef. Medieval-tier melee weapons just use `Neolithic` or `MeleePiercer` and rely on `<techLevel>Medieval</techLevel>` + `<weaponTags><li>MedievalMeleeBasic</li></weaponTags>` for tier identity. Tags are free-form strings used by `PawnGroupMakers` for raid composition; classes are typed defs used by Ideology/role restrictions and weapon-style filters.

Second gotcha when fixing: XML list inheritance REPLACES by default. A child `<weaponClasses><li>Medieval</li></weaponClasses>` overrides the parent abstract's `<weaponClasses><li>MeleePiercer</li></weaponClasses>` rather than appending — so removing the bad line both clears the error AND restores the inherited `MeleePiercer`. Don't try to "merge" it back by hand.

*Why it's tricky:* "Medieval" is a meaningful tier word that shows up everywhere in the codebase (techLevel, weaponTags, faction names), so reaching for it as a weaponClass feels right. The error message names the def by string but doesn't suggest the valid set, and the attribution is `[RimWorld]` (the cross-ref resolver throws) so it doesn't look like a mod-side bug at first glance.

## When auto-placing a Cooler in a freezer wall, Rot4 controls which side gets cold — verify against Building_Cooler.TickRare

`Building_Cooler.TickRare` cools the cell at `Position + IntVec3.South.RotatedBy(Rotation)` and pushes heat to `Position + IntVec3.North.RotatedBy(Rotation)`. So the COLD side is the "south-rotated" cell and HOT exhaust is the "north-rotated" cell.

Recipe for a cooler mounted in a room's NORTH wall (interior is to the south, outside to the north): use **`Rot4.North`** (identity) → cold cell = South (interior gets cold), heat = North (vents outside). Using `Rot4.South` flips it: cold blows OUTSIDE and heat dumps INTO the freezer (the room heats up instead of cooling). General rule: the rotation that makes the *interior* coincide with `Position + South.RotatedBy(Rotation)` is the correct one; for a wall on the room's north edge that's Rot4.North, for the south edge it's Rot4.South, etc.

`PlaceWorker_Cooler.AllowsPlacing` separately requires BOTH the N and S cells be free of impassable things/blueprints — so place the cooler blueprint BEFORE the surrounding wall blueprints, and verify acceptance with `GenConstruct.CanPlaceBlueprintAt(coolerDef, cell, rot, map)` (log `.Reason` and bail if rejected, else you build an empty room shell forever).

*Why it's tricky:* "facing south so cold goes in" sounds right but is exactly backwards — the rotation names the cell the cold comes OUT of relative to the building's own facing, and RotatedBy composes with it. The only reliable way is to read `Building_Cooler.TickRare` and match the interior cell to the `South.RotatedBy(Rotation)` cell. The symptom of getting it wrong: the "freezer" slowly warms above ambient instead of dropping to the target temp, and food spoils anyway.

## Buildings with constructionSkillPrerequisite are never offered to colonists below that skill

Many buildings and some floors carry a `<constructionSkillPrerequisite>` (for example, several power and temperature buildings in `Buildings_Power.xml` / `Buildings_Temperature.xml`, with values from 3 up to 8). If no colonist meets it, the construct job is simply never offered — it's a pre-job gate, not a botch-chance thing. A colony of low-skill builders can finish the skill-0 buildings and then have nothing left it's allowed to build, so it never earns the XP to clear the gate.

*Why it's tricky:* the symptom (a blueprint that nobody builds, pawns idle) looks like a botch loop or a pathing problem, but there's no error and no botch. You won't find it in C#; it's a field on the ThingDef — look for `constructionSkillPrerequisite` in the def's XML under `Data/Core/Defs/`.

## To run an Effecter from a ThingComp, call EffectTick(parent, parent) every CompTick

Store the `Effecter` reference and call `effecter.EffectTick(parent, parent)` (passing the parent Thing as both A and B targets) inside `CompTick()`; this is the pattern vanilla's `CompFireBurst` uses. The parent ThingDef MUST have `<tickerType>Normal</tickerType>` or `CompTick` never runs.

*Why it's tricky:* sub-effecters such as `SubEffecter_SprayerChance` need the parent's position context to spawn motes/flecks correctly, and nothing in Player.log tells you when an effecter is spawning invisibly.

> **Unverified:** That vanilla `CompFireBurst` uses this pattern, and that the `EffecterMaintainer` route renders differently, were not checked. Check this against the decompiler/vanilla defs before relying on it.
