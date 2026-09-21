---
name: address-issue
description: |
    Use when the user asks to address/fix one or more issues listed in an issue file (e.g. BUG-REPORT.md, CONDITION-UPDATE-OPPORTUNITIES.md), given the file, which issue(s) to target, and optional additional instructions. Also use when the user asks to address/fix a GitHub issue by number or URL (`--gh`). Also use when the user gives direct instructions for a change with no issue file or GitHub issue to read from (`--direct`).
    - Fixes the targeted issue(s), formats with csharpier, updates CHANGELOG.md if player-visible, marks the issue(s) fixed with '✅' (file mode) or has commit-message add a 'Fixes #<GH-ISSUE>' line (GitHub-issue mode), and runs the commit-message skill without committing.
    - Any additional instructions provided are also taken into account.
argument-hint: "<ISSUE-FILE> <ISSUE-TARGET> [ADDITIONAL-INSTRUCTIONS...] | --gh <GH-ISSUE> [ADDITIONAL-INSTRUCTIONS...] | --direct <INSTRUCTIONS...>"
---

# Address issue

The raw arguments are: $ARGUMENTS

**Parse the arguments as follows:**

- If the arguments start with `--gh`, this is **GitHub-issue mode**: `<GH-ISSUE>` follows, almost always as `#123` (a GitHub issue number with a leading `#`), but it may also be given as a bare number or a full GitHub issue URL. Everything after it is `[ADDITIONAL-INSTRUCTIONS...]`.
- If the arguments start with `--direct`, this is **direct mode**: everything after it is the instructions describing the change to make. There is no issue file or GitHub issue to read from.
- Otherwise, this is **file mode**: the first token is `<ISSUE-FILE>`, the file to read the issues from. The next token is `<ISSUE-TARGET>`, which issue or set of issues within that file to address.
- If `[ADDITIONAL-INSTRUCTIONS...]` is present in either mode, extract the instruction text. If absent, there is no extra instruction.

## Main task

**GitHub-issue mode:** Run `gh issue view <GH-ISSUE>` (add `--comments` if the discussion may hold useful context) to fetch the issue's title, body, and state from this repo's tracker. Treat its content as the issue to address in place of an `<ISSUE-FILE>`/`<ISSUE-TARGET>` pair. There is no file to mark fixed — skip that step. Do not close, comment on, or otherwise modify the GitHub issue; that's left for the user to do after reviewing the changes.

**File mode:** Address the issues in `<ISSUE-TARGET>` from the file `<ISSUE-FILE>`.

**Direct mode:** Address the change described in the instructions directly; there is no issue file to read from and no file to mark fixed — skip that step.

**All modes:** Do not launch the game to do any verification with GABS except to run unit tests as per CLAUDE.md. If any changes need in-game verification, ask the user to do it and report back to you.

**A clean build is mandatory before you are done.** After the edits (and again after any fixes to build output), build the project for _every_ supported RimWorld version as described in CLAUDE.md's "Verifying a build succeeds" section, using a clean build (not an incremental one) so no warnings are hidden. Read the full output: every error _and_ every warning must be fixed, and you must rebuild until each version reports 0 warnings and 0 errors. Do not proceed to the changelog, marking the issue fixed, or the commit message while any build is failing or warning, and do not report the task as complete without having run these builds.

When done doing your edits and the builds are clean, run `csharpier format .`, then describe the changes made in CHANGELOG.md as a player would understand the changes to be helpful, following the "Writing a changelog entry" rules below. If the changes makes no discernible difference to the player, skip updating the change log. Run the `commit-message` skill. Do not actually do the commit, let the user inspect and review the result.

### Writing a changelog entry

Write a user-facing changelog entry.

Include only changes that affect users of the released version:

- New capabilities
- User-visible behavior changes
- Fixes to problems users could actually encounter
- Important limitations or migration steps

Exclude:

- Unreleased prototypes or previous designs
- Internal implementation details
- Comparisons with unreleased versions
- Rationale, design history, or "instead of" phrasing unless the previous behavior was publicly released
- Details that do not help a user decide whether the update matters to them

Do not infer that a contrast belongs in the changelog merely because it appears in the source material. If a detail would make a reader ask "Why do I need to know that?", omit it.

In file mode, mark the `<ISSUE-TARGET>` set as fixed by prefixing them with '✅', e.g. '✅ BUG-1' or '✅ OPP-007'.

In GitHub-issue mode, instead of marking a file, instruct the `commit-message` skill to append a `Fixes #<GH-ISSUE>` (or `Resolves #<GH-ISSUE>` if that reads more naturally for the change) line to the first line of the commit body, using the bare issue number (e.g. `Fixes #123`) regardless of how `<GH-ISSUE>` was given on input.

If anything needs clarification or there are multiple ways to do something, use the AskUserQuestion tool to let the user inform you or decide which choice to make, do not just make assumptions and think that's what he user would've wanted.

If any `[ADDITIONAL-INSTRUCTIONS...]` were present, adhere to them as best you can.

Use a todo list to track each step listed above; here's an example from a prior manual address issue session:

- Fix BUG-1 (Scribe_Either.LookValue missing write-backs)
- Fix BUG-2 (BodyPart null guard)
- Fix BUG-27 (LeftAnd/RightAnd docs)
- Clean-build project for all supported versions (1.3, 1.4, 1.5, 1.6); fix all errors AND warnings and rebuild until 0 warnings, 0 errors on every version
- Run the test suite via GABS (RimTest Redux or DevTools, whichever the project uses; see `gabs.md`). With DevTools, run only filtered subsets (`testFilter`) while iterating, and do one unfiltered run of all tests as the final regression run before considering the implementation done
- Run csharpier format .
- Update CHANGELOG.md
- Mark bugs fixed in BUG-REPORT.md
- Run commit-message skill
