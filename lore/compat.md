## Don't auto-disable opt-in features on Harmony-patch conflict — warn and let the user toggle

When an opt-in mod feature replaces a vanilla UI method via Harmony (e.g. a prefix on `DoWindowContents` that returns false), the obvious safety net is "scan `Harmony.GetPatchInfo` for foreign patches on the same method, and auto-disable my feature if any are found". This is too cautious. Most foreign Harmony patches DON'T actually break visible compositing — they may capture rects, draw highlights, fire telemetry, etc. Auto-disabling silently hides the feature from a large fraction of users who'd otherwise be fine.

Better pattern: keep the conflict scan, but make it **diagnostic only**.

- The feature's enabled check reads ONLY the user's settings flag, never the scan result.
- The scan runs at startup, stores its result in a separate flag for the settings UI, and emits ONE `Log.Message` — "Heads-up: another mod has patched X. <Feature> is still on; turn it off in mod settings if you see glitches."
- The settings UI always shows the toggle, and when the scan flagged a conflict, draws a yellow `⚠` heads-up line under the checkbox.

The user keeps control: their toggle does what it says. If the other mod's patch actually breaks things, they see it visually and flip the toggle off themselves.

*Why it's tricky:* the auto-disable is *technically* the safer default, but in practice most Harmony patches coexist fine, and "feature silently doesn't work and there's an obscure log message explaining why" is the worst possible UX — the user just thinks the mod is broken. Trust the user to flip a toggle if they see actual problems.
