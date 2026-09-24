## Triage Player.log errors before fixing — many are from OTHER mods

Cross-reference errors like `Could not resolve cross-reference: No Verse.ThingDef named X found` name the *target* def, not the source mod. Grep your active mod's source for that defName — if zero hits, the error belongs to another loaded mod and is noise. Tell the user explicitly ("these are from <other mod>, your mod is clean") rather than triaging them.

*Why it's tricky:* the log mixes all loaded mods together and a freshly-loaded mod easily produces a wall of unrelated XML errors that look damning at first glance. Reflex: debug the first error you see. Right reflex: grep your own source first. Wasted hours come from chasing other people's bugs.

## Dev-menu "advance time" jumps TicksGame and bypasses MakeIntervalIncidents — drive once-per-day work from GameComponentUpdate with a catch-up loop

RimWorld's dev menu "advance time by N days" / "pass time" actions call `Find.TickManager.DebugSetTicksGame(TicksGame + duration)`. This directly increments the tick counter **without running simulation**, so:

- `StorytellerComp.MakeIntervalIncidents` never fires for the skipped time.
- `GameComponent.GameComponentTick` never fires either.
- Anything you've gated on a per-tick or per-interval cadence (periodic recomputes, world simulation steps, raid generation, once-per-day housekeeping) **silently skips the entire skipped window.**

When a tester reports "I advanced 15 days and nothing in my mod changed," this is almost always the cause.

(Vanilla has no "daily tick" hook; "once per day" work here means logic you write yourself that should run once for each in-game day.)

Fix has two parts:

1. **Drive once-per-day work from `GameComponent.GameComponentUpdate`**, not from a storyteller comp. `GameComponentUpdate` fires every Unity frame regardless of pause/tick state, so after a `DebugSetTicksGame` jump the next frame runs your check. Cache the last day you checked so repeated calls within the same in-game day are cheap.

2. **Make the per-day work a catch-up loop.** If your handler stores the last day it processed and early-outs when `GenDate.DaysPassed` equals it, a 15-day jump only runs it ONCE — losing 14 days of chance-based work and modulo-N cadence windows like `day % 5 == 0`. Instead, keep a scribed `lastProcessedDay` and process every day in between:

```csharp
// In a GameComponent: lastProcessedDay is a scribed int, initialised to -1.
const int MaxCatchUpDays = 60;

void CatchUpDays() {
    int today = GenDate.DaysPassed;
    if (lastProcessedDay < 0) lastProcessedDay = today - 1; // fresh save guard
    int processed = 0;
    while (lastProcessedDay < today && processed < MaxCatchUpDays) {
        lastProcessedDay++;
        try { ProcessDay(lastProcessedDay); }
        catch (Exception e) { Log.Error($"ProcessDay({lastProcessedDay}): {e}"); }
        processed++;
    }
    if (lastProcessedDay < today) lastProcessedDay = today; // beyond the cap, skip the rest
}
```

The cap matters — without it, loading a 5-year-old save would lock the game for minutes replaying every day.

*Why it's tricky:* the storyteller-comp interval path is the documented place to put periodic work, and it works fine for normal play. The bug only surfaces when a dev-tester (or a save loaded after a long break) advances time non-incrementally. Your code looks correct, your save scribes correctly, and on a fresh natural-play test everything fires; but a tester who fast-forwards sees nothing.

> **Unverified:** Only the existence of `DebugSetTicksGame` and `GenDate.DaysPassed` was checked; that the dev-menu actions call it and skip storyteller/component ticks was not. Check this against the decompiler/vanilla defs before relying on it.
