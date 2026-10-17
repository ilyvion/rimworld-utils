---
name: sync-documentation
description: |
    Use when the user asks to bring a mod's user-facing documentation (README.md, SteamDescription.txt, and similar introductory/overview docs) up to date with the current state of the mod, or to check whether such documents are in sync with each other and with reality.
    - Audits the changelog and commit history to find significant, user-facing changes, then updates the target documents to reflect the mod's current state.
    - Does not turn the documents into a changelog or exhaustive feature list; keeps existing tone and structure.
argument-hint: "[DOC-FILE...] [ADDITIONAL-INSTRUCTIONS...]"
---

# Sync documentation

The raw arguments are: $ARGUMENTS

**Parse the arguments as follows:**

- Any tokens that name existing files in the repo are `<DOC-FILE...>`, the documents to bring up to date. If none are given, default to whichever of `README.md`, `SteamDescription.txt`, and `About/About.xml`'s `<description>` element exist at the repo root.
- Everything else is `[ADDITIONAL-INSTRUCTIONS...]`. If present, adhere to it as best you can alongside the rest of this task.

## Main task

Bring `<DOC-FILE...>` up to date with the current state of the mod.

To understand how the mod has evolved, at minimum:

- Read the changelog.
- Review the commit message history to identify significant features, changes, fixes, and removed/changed behavior over time.
- Do not inspect every commit's file diff unless you need to verify a specific detail. The commit messages and changelog are the primary sources for this audit.
- Check the current code/configuration where necessary to confirm that something mentioned in a document is still accurate.

Update the documentation to reflect the current, user-relevant state of the mod. Focus on things a new or prospective user would reasonably want to know: what the mod does, its important features, notable behavior, requirements/limitations, and anything else that is useful for understanding or using it.

Do not turn the documents into a changelog or exhaustive feature list. These are introductory/broadly explanatory documents, not detailed technical documentation. Prioritize significant and user-facing changes over minor bug fixes, refactors, formatting changes, and other implementation details.

Keep the existing tone and structure where they still work, and avoid adding information that you cannot verify. The goal is for someone reading any of `<DOC-FILE...>` today to get an accurate, reasonably concise overview of the mod as it currently exists.

If more than one document is in scope, also check them against each other for consistency — same facts, no contradictions — even where wording or level of detail reasonably differs between them (e.g. `SteamDescription.txt` is typically terser than `README.md`).

`SteamDescription.txt` must not mention which RimWorld versions the mod supports at all. Remove any such mention you find there, and don't copy one over from another document.

If anything needs clarification, or there are multiple reasonable ways to resolve an inconsistency, use the AskUserQuestion tool rather than assuming.
