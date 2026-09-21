---
name: rimworld-testing-opportunities
description: "Find more testing opportunities in a RimWorld mod project and produce a report. Invoke with /rimworld-testing-opportunities or /rimworld-testing-opportunities [--file <filename>]."
argument-hint: "[--file <filename>]"
---

# Task: Find more testing opportunities in a RimWorld mod project

## Arguments

The raw arguments are: $ARGUMENTS

If `--file <filename>` is present, extract the filename that follows it and store it as `OUTPUT_FILE`. If it is absent, the report is only presented to the user and nothing is written to disk.

## Context

This is a RimWorld mod. It may be organized as a single source project or split across several (e.g. a core project plus separate projects for individual features or integrations with other mods). Discover the actual project layout under `Source/` (or wherever the `.sln` points) before searching — don't assume names or a specific count of projects.

A dev-only test project (conventionally `<ModName>.Tests`) may already exist, run in-process/in-game via the sibling **RimTest Redux** mod or via **DevTools** (SmashPhil; `CLAUDE.md` says how to tell which). If `CLAUDE.md` documents the test-writing mechanics for this repo (RimTest Redux or DevTools, a Debug-only build split, `InternalsVisibleTo` + extraction patterns, etc.), **read that section before doing anything else** — it's required background, not optional reading.

DevTools runs tests inside a live game (`[TestFixture(TestType.Playing)]`), so with DevTools, logic that needs a running `Map`, `Find.CurrentMap`, ticking or live `Pawn`/`Thing` state is testable as an in-game test. RimTest Redux and unit-only setups don't offer that.

If test suites already exist, review them first so you don't propose tests for what's already covered.

**There is likely a lot more testable logic left.**

## Your task

**This is a research task, not an implementation task. Do not write any test code or modify any production code.** Your sole output is a report (presented to the user, and saved to `OUTPUT_FILE` when given).

Search the entire codebase (every source project, not just the most obviously feature-heavy one) for logic worth testing. Don't restrict the search to code that's already pure — RimWorld mod logic is routinely entangled with `Verse`/`Unity`/`RimWorld` live-game types (`Pawn`, `Map`, `Thing`, `ThingDef`, `Building`, etc.), and that entanglement alone isn't a reason to skip it. Look for three categories:

- **Pure or near-pure logic entangled with live-game state or side effects** (branching, comparisons, sorting, filtering, threshold/limit math, aggregation, state-machine transitions, coroutine/generator plumbing with embedded decisions) that isn't unit-testable as-is but could be made so by extracting the decision logic into an `internal static` method — generic over plain types/delegates instead of live game types where the values involved are simple enough to represent that way (numbers, strings, enums, simple records/structs).
- **Logic that fundamentally needs a complex live-game type as input** (e.g. something that must inspect a `Pawn`'s skills, a `Map`'s zones, a `ThingDef`'s stats) where the type itself can't be swapped for a primitive, but a **mockable seam** is still plausible: an interface or delegate the method could depend on instead of the concrete Verse/Unity type, a wrapper/adapter that exposes just the needed surface, or constructing a real-but-minimal instance of the type outside a live game (many Verse types are plain C# objects that can be `new`'d and populated without a running `Game`/`Map`). Don't dismiss these as untestable — sketch the seam. Prefer extracting pure logic or adding a seam whenever that is reasonably possible, since those tests are faster and more focused. With DevTools, logic that still needs runtime-only state (an active `Map` simulation tick, `Find.CurrentMap`, live UI/rendering) is a valid candidate for an in-game test; list it as such, and only exclude it if it can't be driven from a test at all. Without DevTools, exclude it when there is no reasonable way to fake or wrap that state.
- **Already pure and testable as-is** (a plain static/instance method with no game-state dependency) but simply has no test yet.
- **Historically buggy**: check `CHANGELOG.md` and git history (`git log --oneline`) for past bug fixes — logic that was wrong once is exactly the kind of thing that benefits most from a regression-guard test, especially if this repo's `CLAUDE.md` has an "every code change needs a test" rule.

Be thorough. Don't stop at the first few obvious candidates — walk through each significant class across every source project (job/manager/controller classes, trigger/condition classes, UI-tab or window classes with non-trivial logic behind the rendering, settings, utilities/helpers, and any integration projects with other mods). Grep for patterns like `OrderBy`, `Where`, `.Sum(`, `.Count(`, comparison operators against thresholds, and coroutine methods with embedded decision logic — these are strong signals of testable-but-untested logic. Also check any `Helpers/`/`Utilities/`-style directories in each project.

For each candidate you find, verify:
- You've actually located the real method signature and behavior by reading the file, not by guessing from a name.
- If it depends on a live-game type, you've sketched concretely how it could be tested — extraction to plain types, an interface/delegate seam, direct construction of a minimal real instance, or (with DevTools) an in-game test that sets up the real state — rather than just asserting it's testable. Prefer the options in that order.

## Output

Present the report to the user. If `OUTPUT_FILE` was set, also write the report to that file (use the Write tool; relative paths resolve against the repo root) and confirm, e.g. `Report saved to OUTPUT_FILE.` Be detailed enough that another agent, with no prior context on this investigation, can pick any single entry and start writing a test immediately without re-reading the whole codebase. For each opportunity, include:

- **Location**: file path and method/class name (with line numbers if practical).
- **What it does**: a short, concrete description of the current behavior.
- **Why it's testable / what seam (if any) it needs**: e.g. "already pure, no extraction needed"; "needs `internal static` extraction to plain types, because it currently calls `pawn.training.learned` directly"; or "needs a mockable interface/delegate around `IHaulDestination`, or can be tested by constructing a bare `Pawn`/`ThingDef` instance without a running `Map`"; "needs an in-game DevTools test that spawns a pawn on the current map and ticks it, because no pure part can be extracted."
- **What to test**: specific cases/boundaries worth asserting (edge cases, off-by-one boundaries, zero/negative/empty inputs, past-bug regression cases if applicable).
- **Priority/risk signal**: note if this logic has a history of bugs (cite the CHANGELOG entry or commit if so), is complex/error-prone, or is simple boilerplate not worth prioritizing.

Group entries by source project, then by subsystem/feature within each project, so the document is easy to navigate. Do not include entries that duplicate existing test suites — this report is about *new* ground.
