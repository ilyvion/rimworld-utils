None of these mods ship hand-drawn icon assets — new UI icons (toolbar buttons, status indicators, chevrons, etc.) are generated via an SVG → PNG pipeline. This was established in RimTest's `Common/Textures/UI/` (see `RTRIconChevronCollapsed.svg`-style sources and `Source/RimTestRedux/Core/Icons.cs`/`StatusStyle.cs` for the resulting texture fields and color mapping), but the workflow applies to any mod in this family:

1. Write a source SVG at 64x64 (`viewBox="0 0 64 64"`), simple shapes (`polyline`, `path`, etc.) on a transparent background.
    - Neutral action/navigation icons (run, search, collapse/expand, chevrons — anything that isn't a status indicator) use a neutral stroke/fill color matching the game's UI text color (e.g. `#E4E1D8`).
    - Chevrons/arrows use `stroke-linecap="round"` (and `stroke-linejoin="round"`) for a softer look; `stroke-width="8"` at this viewBox reads well at in-game sizes.
    - Status icons (pass/fail/skip/unknown, etc.) use color-coded fills instead of the neutral color, matching whatever semantic color palette the mod already defines for that status.
2. Rasterize with ImageMagick: `magick -background none -density 384 <icon>.svg -resize 64x64 <icon>.png`. The high `-density` before downscaling gives clean anti-aliased edges instead of a blocky rasterization.
3. Copy the resulting PNG into the mod's `Common/Textures/UI/` (or equivalent) directory, then add a `ContentFinder<Texture2D>.Get("UI/IconName")` field for it wherever that mod centralizes its texture references (e.g. `Icons.cs`).
4. Copy the SVG next to the PNG so it can be used for reference or to make adjustments later.

**Naming**: RimWorld's paths are a shared namespace across every installed mod, so a generic name like `icon_log.png` risks colliding with another mod's asset. Prefix filenames with a short per-mod code (RimTest Redux uses `RTR`) and use RimWorld's own `PascalCase` convention instead of `snake_case` — e.g. `RTRIconLog`, `RTRIconStatusPass`, `RTRIconChevronCollapsed`.
