---
name: address-issue
description: |
  Use when the user asks to address/fix one or more issues listed in an issue file (e.g. BUG-REPORT.md, CONDITION-UPDATE-OPPORTUNITIES.md), given the file, which issue(s) to target, and optional additional instructions.
  - Fixes the targeted issue(s), formats with csharpier, updates CHANGELOG.md if player-visible, marks the issue(s) fixed with '✅', and runs the commit-message skill without committing.
  - Any additional instructions provided are also taken into account.
argument-hint: "<ISSUE-FILE> <ISSUE-TARGET> [ADDITIONAL-INSTRUCTIONS...]"
---

# Address issue

The raw arguments are: $ARGUMENTS

**Parse the arguments as follows:**

- The `<ISSUE-FILE>` is the file to read the issues from.
- The `<ISSUE-TARGET>` is which issue or set of issues to address.
- If `[ADDITIONAL-INSTRUCTIONS...]` is present, extract the instruction text. If absent, there is no extra instruction.

## Main task

Address the issues in `<ISSUE-TARGET>` from the file `<ISSUE-FILE>`. Do not launch the game to do any verification with GABS except to run unit tests as per CLAUDE.md. If any changes need in-game verification, ask the user to do it and report back to you.

When done doing your edits, run `csharpier format .`, then describe the changes made in CHANGELOG.md as a player would understand the changes to be helpful. If the changes makes no discernible difference to the player, skip updating the change log. Run the `commit-message` skill. Do not actually do the commit, let the user inspect and review the result.

Mark the `<ISSUE-TARGET>` set as fixed by prefixing them with '✅', e.g. '✅ BUG-1' or '✅ OPP-007'.

If any `[ADDITIONAL-INSTRUCTIONS...]` were present, adhere to them as best you can.

Use a todo list to track each step listed above; here's an example from a prior manual address issue session:

* Fix BUG-1 (Scribe_Either.LookValue missing write-backs)
* Fix BUG-2 (BodyPart null guard)
* Fix BUG-27 (LeftAnd/RightAnd docs)
* Build project for all supported versions (1.3, 1.4, 1.5, 1.6)
* Run RimTest suite via GABS
* Run csharpier format .
* Update CHANGELOG.md
* Mark bugs fixed in BUG-REPORT.md
* Run commit-message skill
