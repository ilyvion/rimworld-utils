---
name: find-rimworld-mod
description: "Locate an installed RimWorld mod's directory or Steam workshop id from a (partial) mod name. Use when the user says things like 'find me the hygiene mod' (invoke with 'hygiene' as the mod name), asks where a mod lives or what its workshop id is, or when you need to locate a mod's assemblies, Defs, textures, or other files — locating the mod's directory first makes finding them straightforward."
---

# Finding a RimWorld mod

Input: a (partial) mod name, e.g. `hygiene` or `dubs`. Matching is case-insensitive and the fragment may appear anywhere in the mod's `<name>`.

Workshop mods live under `/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/workshop/content/294100`. Each subdirectory is named after the mod's Steam workshop id and contains `About/About.xml`.

## Procedure

Run, substituting the mod name fragment for `MOD_NAME`:

```bash
find /home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/workshop/content/294100 -name 'About.xml' -exec rg -iH "<name>.*MOD_NAME" {} \;
```

This scoped `find` is fine; **do not broaden it to other roots**. Each match prints two lines: the `About.xml` path, then the matching `<name>` line:

```
/home/alex/.var/app/com.valvesoftware.Steam/.steam/steam/steamapps/workshop/content/294100/836308268/About/About.xml
4:	<name>Dubs Bad Hygiene</name>
```

The number directly after `294100` is the workshop id; the mod directory is the path up to and including that id (strip `/About/About.xml`).

If nothing matches, retry with a shorter or differently spelled fragment (e.g. drop spaces or punctuation, use a single distinctive word). If still nothing, tell the user the mod isn't in the workshop directory; it may be a local mod (in RimWorld's `Mods` folder) — **ask the user for its location rather than searching the filesystem.**

## Output

For every match, report:

```
Mod: <full name from <name>>
Steam Workshop ID: <id>
Path: <absolute mod directory>
```

Separate multiple mods with a blank line. If the user's request implied a single mod but several match, list all of them and say which looks most likely.

When the goal is finding assemblies or other files, use the mod directory as the search root afterward. Assemblies are usually in `<Path>/<version>/Assemblies` (e.g. `1.6/Assemblies`), `<Path>/Common/Assemblies`, or `<Path>/Assemblies`; check `<Path>/LoadFolders.xml` for locations to search if present.
