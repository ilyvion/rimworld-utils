## "Leak suspected in object pool for PawnPaths" — bulk-spawning past the pool cap

`Leak suspected in object pool for PawnPaths, created: 40, expected less than 39. Dispose them so they can be reused by the pool.` fires when a mod spawns enough pawns in a single tick that every spawned pawn requests a `PawnPath` before any prior path is recycled. RimWorld's `PawnPathPool` defaults to ~39 active paths; once you exceed that on the same tick the engine assumes a leak rather than a legitimate horde.

This is a **warning, not a fatal error** — the mod still works and the pawns still move. But it pollutes the log every spawn and indicates the spawn is fighting the engine's assumptions.

Mitigations (in increasing order of effort):

1. **Stagger the spawn across ticks.** Instead of a `for` loop that calls `GenSpawn.Spawn` 100×, schedule the spawns via a `MapComponent` or one-shot `LongEventHandler`/`TickManager` callback that drips e.g. 10 pawns per tick over 10 ticks. The pool recycles between ticks, so the warning never fires.
2. **Use `PawnsArrivalModeWorker_EdgeWalkIn`-style entry.** Vanilla raids of 100+ pirates don't trip this warning because the arrival worker spawns at the map edge with a brief delay between pawns; copying that pattern (or calling `IncidentWorker_Raid`'s helpers when applicable) gets the staggering for free.
3. **Skip pathing entirely on spawn.** If your pawns don't need a path immediately (they'll wander/idle for a tick), set their initial job to `JobDefOf.Wait` or similar — no `PawnPath` request, no pool pressure.

*Why it's tricky:* the warning says "leak" but you're not leaking — you're legitimately using more paths than the pool was tuned for. Search for `PawnPathPool` in C# source if you're tempted to patch the cap; staggering is the cleaner fix.

> **Unverified:** The pool cap of ~39 and the `PawnsArrivalModeWorker_EdgeWalkIn` staggering claim were not checked. Check this against the decompiler/vanilla defs before relying on it.

## C# performance work — patch the right method, not the obvious one

When optimizing pawn behavior at scale (100+ pawns), the bottleneck is usually not the obvious one. The expensive paths in `Pawn` ticking, in order:

1. `Pawn_NeedsTracker.NeedsTrackerTickInterval` (every need updates every 150 ticks)
2. Think tree evaluation (`Pawn_JobTracker` / `ThinkNode`)
3. `Pawn_PathFollower.PatherTick` (moving pawns only)
4. `Pawn_HealthTracker.HealthTick` / `HealthTickInterval` (only matters if Hediffs are stacked deep)

If your design lets pawns skip needs entirely (mechs, custom races with `<race><needsRest>false</needsRest>` etc.), patching `Pawn_NeedsTracker.ShouldHaveNeed` to short-circuit on your kind is usually the single biggest win. A Harmony postfix returning `false` for any of `Food`/`Rest`/`Joy`/`Beauty`/`Comfort` skips that need's ticking entirely.

*Why it's tricky:* the obvious target (`Pawn.Tick`) is mostly dispatching to subsystems; patching it doesn't help. The expensive work is in the trackers it calls.

> **Unverified:** The cost ranking of the trackers and the claim that skipping needs is the biggest win were not measured; only the 150-tick need interval was checked. Check this against the decompiler/vanilla defs before relying on it.

## MapComponentTick freezes during pause — use MapComponentUpdate for player-facing work

`MapComponent.MapComponentTick()` only fires while the game is ticking — it does NOT fire while paused. `Find.TickManager.TicksGame` is frozen during pause too. Any system that the player triggers during pause (dev-mode spawns, inspect-pane clicks, watching the colonist bar before unpause, mod-options actions) will see its tick-based dispatch HANG indefinitely. UI animations using `Time.realtimeSinceStartup` keep going so the user sees spinners / loading indicators / etc., but the actual work behind them never advances. Symptom: "the spinner spins forever but nothing happens, until I unpause and then it works".

**Fix:** override `MapComponentUpdate()` instead — fires every Unity frame regardless of pause. Replace tick-count throttling (`nextTick = TicksGame + DelayTicks`) with realtime throttling (`nextProcessRealtime = Time.realtimeSinceStartup + DelayRealSeconds`). Use `MapComponentTick` only when the work IS meant to be gated by in-game time (mood ticks, hediff progression, plant growth, etc.).

*Why it's tricky:* every example in the wild uses `MapComponentTick`, so it feels like the canonical choice. The pause failure mode only surfaces when a real user starts hammering dev tools during testing — which is exactly when you don't notice it because the right-click → manual-trigger path works fine and you blame your queue logic. The actual culprit is the framework method choice. The diagnosis tell: "manual path works, queue/background path hangs, spinners run".

## When caching an AccessTools.TypeByName result that can be null, use a separate _searched bool

The pattern `if (cached != null) return cached; cached = AccessTools.TypeByName("X"); return cached;` silently breaks when the type is absent: `cached` stays `null` forever and `AccessTools.TypeByName` (a full AppDomain type scan, O(all types in all loaded assemblies)) fires on **every call**, causing severe frame-rate drops when called from per-frame code.

Fix: add a separate `bool _xSearched` sentinel flag:
```csharp
static bool _xSearched;
static Type? _xType;
static Type? GetXType() {
    if (!_xSearched) { _xSearched = true; _xType = AccessTools.TypeByName("Ns.X"); }
    return _xType;
}
```

*Why it's tricky:* the null-check cache appears correct and the bug only manifests when the optional mod is absent — which usually isn't tested until users remove the dependency.

## When prefixing a per-tick engine method like Projectile.Tick, gate on a cached per-map flag before any pawn scan

A Harmony prefix on `Projectile.Tick` (or any per-tick, per-instance engine method) runs an enormous number of times — once per projectile per tick. Never do a `map.mapPawns.AllPawnsSpawned` loop or a LINQ `.OfType<T>().FirstOrDefault()` hediff scan inside it. Even gating on a world-level condition (e.g. "any legendary exists anywhere") is wrong, because a distant unrelated entity makes every projectile on the player's map pay the cost.

Recipe: maintain a cheap per-map boolean on a `MapComponent` (refresh it in `MapComponentTick`, which runs once/tick regardless of projectile count) and have the prefix read that single bool first, bailing instantly when false. Only do the expensive proximity scan once the cheap gate confirms it's relevant on *this* projectile's map.

*Why it's tricky:* it compiles fine and works in early-game testing where few projectiles fly; the framerate cliff only appears in late-game firefights (mortar barrage + large raid + the triggering entity present) — exactly the dramatic moment the feature exists for. Also prefer an allocation-free `for` loop over `hediffSet.hediffs` to `OfType<T>().FirstOrDefault()` in any per-frame/per-tick path — the LINQ form allocates an iterator every call.
