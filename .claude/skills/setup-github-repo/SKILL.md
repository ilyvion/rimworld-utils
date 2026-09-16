---
name: setup-github-repo
description: |
    Use when the user asks to set up, create, or initialize a GitHub repo for the current project using `gh` (e.g. "set up a repo on github for this project using gh").
    - Creates the GitHub repo and wires up the local `origin` remote; commits tracked changes if asked to.
    - Never pushes, and never runs `git push` or `gh repo create --push` under any circumstance — creating the remote is the entire scope of this skill, pushing is always left to the user.
argument-hint: "[ADDITIONAL-INSTRUCTIONS...]"
---

# Set up a GitHub repo

The raw arguments, if any, are: $ARGUMENTS

Treat any given text as `[ADDITIONAL-INSTRUCTIONS...]` — additional instructions to adhere to alongside the rest of this task.

## Scope

This skill's job ends at "the repo exists on GitHub and `origin` points at it locally." It does not push. Do not run `git push`, do not pass `--push` to `gh repo create`, and do not use any other command or flag that would send commits to the remote — not even as a natural-seeming last step, not even if it would make the task feel "finished." The user asking you to set up a repo is not the same as the user asking you to publish their code; those are separate decisions, and only the user makes the second one, when they're ready. If you find yourself about to run a push, stop and just report that the repo is ready to push whenever they choose.

## Main task

1. **Survey current state first, before creating anything:**
   - `gh auth status` — confirm `gh` is authenticated. If not, tell the user and stop.
   - `git remote -v` — if a remote already exists (`origin` or otherwise), do not create a new repo. Report what's already there and ask the user what they actually want (a different remote name? point `origin` elsewhere? nothing to do?) rather than assuming.
   - `git status --short --ignored` — see what's tracked, untracked, and ignored.

2. **Determine the intended repo name and owner:**
   - Look for existing hints: `grep -rn "github.com" README.md CHANGELOG.md About/About.xml` (or equivalent project files) for a URL that already names the intended repo.
   - If nothing pins it down, default to a kebab-case version of the project directory name, and default the owner to whatever account the user's other sibling repos under this same parent directory use (check a sibling's `git remote -v` if any are available for comparison).
   - Confirm the resulting `<owner>/<name>` and visibility (public/private — default to whatever sibling repos use, or ask if there's no precedent) with the user via AskUserQuestion before creating anything, unless it's already unambiguous from the above.

3. **Handle untracked working/scratch files before committing anything:**
   - Look for files that read as personal working notes rather than project deliverables (e.g. `HANDOVER.md`, scratch issue-tracking files, draft notes) among the untracked files from step 1.
   - Don't silently commit them and don't silently exclude them either — ask the user via AskUserQuestion whether each should be committed, gitignored, or left untracked as-is.

4. **Create the repo:**
   - `gh repo create <owner>/<name> --public|--private --source=. --remote=origin` (no `--push`).
   - Verify the remote landed correctly with `git remote -v`.

5. **Commit, if there's anything ready to commit and the user's answers in step 3 call for it:**
   - Stage what should be staged (excluding anything the user chose to keep untracked/gitignored), following this project's normal commit conventions (see the `commit-message` skill if one is available).
   - Do not push.

6. **Report what was done:** the repo URL, that `origin` is wired up locally, and that pushing is entirely up to the user whenever they're ready. If any `[ADDITIONAL-INSTRUCTIONS...]` were given, make sure they were addressed and mention how.
