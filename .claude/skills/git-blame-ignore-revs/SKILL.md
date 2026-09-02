---
name: git-blame-ignore-revs
description: |
    Use when the user asks to update, refresh, or regenerate this repo's `.git-blame-ignore-revs` file, or asks to find commits that should be ignored by `git blame`.
    - Scans git history for purely mechanical commits (formatting, lint fixes, whitespace-only changes) and writes their hashes to `.git-blame-ignore-revs`, each with a brief comment explaining why it's ignored.
    - Conservative: excludes commits that mix mechanical and substantive changes unless the substantive part is absent or clearly separable.
---

# Update .git-blame-ignore-revs

Go through the repository's Git history and update the `.git-blame-ignore-revs` file containing commits that are safe to ignore when using `git blame`.

## What counts as ignorable

Include commits whose changes are purely mechanical and do not meaningfully alter application behavior or source-level intent, such as:

- Running formatters or reformatting code
- Fixing lint/clippy warnings without changing behavior
- Automated style or whitespace changes
- Other large-scale mechanical transformations that would make `git blame` noisy

Do **not** include commits that contain meaningful functional, architectural, or behavioral changes, even if they also contain formatting or lint fixes.

## How to evaluate candidates

For each candidate commit, inspect the actual diff rather than relying solely on its commit message. Be conservative when a commit mixes mechanical changes with substantive changes: only include it if the substantive changes are absent or clearly separable.

## Output

Write the resulting commit hashes, one per line, to `.git-blame-ignore-revs`, with a brief comment above each commit explaining why it is being ignored.

Also add the candidates you decided to exclude, but leave those commit-hash lines commented out, with a comment above it explaining why each was left out.

Do not modify any other files.
