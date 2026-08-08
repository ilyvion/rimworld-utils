---
name: address-issue
description: |
    Use when the user asks to address/fix one or more issues listed in an issue file (e.g. BUG-REPORT.md, CONDITION-UPDATE-OPPORTUNITIES.md), given the file, which issue(s) to target, and optional additional instructions. Also use when the user asks to address/fix a GitHub issue by number or URL (`--gh`).
    - Fixes the targeted issue(s), formats with csharpier, updates CHANGELOG.md if player-visible, marks the issue(s) fixed with '✅' (file mode) or has commit-message add a 'Fixes #<GH-ISSUE>' line (GitHub-issue mode), and runs the commit-message skill without committing.
    - Any additional instructions provided are also taken into account.
argument-hint: "<ISSUE-FILE> <ISSUE-TARGET> [ADDITIONAL-INSTRUCTIONS...] | --gh <GH-ISSUE> [ADDITIONAL-INSTRUCTIONS...]"
---

# Address issue

The raw arguments are: $ARGUMENTS

**Parse the arguments as follows:**

- If the arguments start with `--gh`, this is **GitHub-issue mode**: `<GH-ISSUE>` follows, almost always as `#123` (a GitHub issue number with a leading `#`), but it may also be given as a bare number or a full GitHub issue URL. Everything after it is `[ADDITIONAL-INSTRUCTIONS...]`.
- Otherwise, this is **file mode**: the first token is `<ISSUE-FILE>`, the file to read the issues from. The next token is `<ISSUE-TARGET>`, which issue or set of issues within that file to address.
- If `[ADDITIONAL-INSTRUCTIONS...]` is present in either mode, extract the instruction text. If absent, there is no extra instruction.

## Main task

**GitHub-issue mode:** Run `gh issue view <GH-ISSUE>` (add `--comments` if the discussion may hold useful context) to fetch the issue's title, body, and state from this repo's tracker. Treat its content as the issue to address in place of an `<ISSUE-FILE>`/`<ISSUE-TARGET>` pair. There is no file to mark fixed — skip that step. Do not close, comment on, or otherwise modify the GitHub issue; that's left for the user to do after reviewing the changes.

**File mode:** Address the issues in `<ISSUE-TARGET>` from the file `<ISSUE-FILE>`.

**Both modes:** Do not launch the game to do any verification with GABS except to run unit tests as per CLAUDE.md. If any changes need in-game verification, ask the user to do it and report back to you.

When done doing your edits, run `csharpier format .`, then describe the changes made in CHANGELOG.md as a player would understand the changes to be helpful. If the changes makes no discernible difference to the player, skip updating the change log. Run the `commit-message` skill. Do not actually do the commit, let the user inspect and review the result.

In file mode, mark the `<ISSUE-TARGET>` set as fixed by prefixing them with '✅', e.g. '✅ BUG-1' or '✅ OPP-007'.

In GitHub-issue mode, instead of marking a file, instruct the `commit-message` skill to append a `Fixes #<GH-ISSUE>` (or `Resolves #<GH-ISSUE>` if that reads more naturally for the change) line to the first line of the commit body, using the bare issue number (e.g. `Fixes #123`) regardless of how `<GH-ISSUE>` was given on input.

If anything needs clarification or there are multiple ways to do something, use the AskUserQuestion tool to let the user inform you or decide which choice to make, do not just make assumptions and think that's what he user would've wanted.

If any `[ADDITIONAL-INSTRUCTIONS...]` were present, adhere to them as best you can.

Use a todo list to track each step listed above; here's an example from a prior manual address issue session:

- Fix BUG-1 (Scribe_Either.LookValue missing write-backs)
- Fix BUG-2 (BodyPart null guard)
- Fix BUG-27 (LeftAnd/RightAnd docs)
- Build project for all supported versions (1.3, 1.4, 1.5, 1.6)
- Run RimTest suite via GABS
- Run csharpier format .
- Update CHANGELOG.md
- Mark bugs fixed in BUG-REPORT.md
- Run commit-message skill
