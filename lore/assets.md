## Vanilla paths are valid — point at them instead of copying art

RimWorld's asset lookup is global across Core + DLC. Pointing `<texPath>Things/Item/Apparel/Shirt_Plain</texPath>` at the vanilla `Shirt_Plain` art is supported and idiomatic for reskin-style mods: the def keeps stats/labels custom while the art stays vanilla.

A file at the same path inside your mod shadows the vanilla asset at runtime, so don't ship a placeholder under a vanilla path.

## Pick descriptive path stems

Namespace your texture/audio paths under a folder named for your mod so they don't collide with vanilla or other mods, and prefer descriptive stems:

- ✅ `UI/YourMod/Settings/MainButton`
- ❌ `UI/Icon` (collides with everything, says nothing)

## Placeholder .ogg files must be valid Vorbis streams, not empty files

Empty/truncated `.ogg` files produce `FMOD error: Unsupported file or audio format.` at game start. A placeholder must be a valid (silent) Vorbis stream. Generate one with `ffmpeg -f lavfi -i anullsrc=r=22050:cl=mono -t 0.5 -c:a libvorbis out.ogg`.

*Why it's tricky:* "silent placeholder" ≠ "valid empty Vorbis stream". FMOD parses the header at load and rejects truncated files; the error noise then pollutes every test session and obscures real errors.

## When a new WorldObjectDef expandingIconTexture path 404s, use a known Core path or your own bundled icon

A mistyped or DLC-only `<expandingIconTexture>` path 404s every frame the world map is rendered, climbing into the thousands of errors per second.

Also: vanilla `World/WorldObjects/Sites/GenericSite` exists as a `<siteTexture>` value (SitePartDef), NOT as an `<expandingIconTexture>` value. The two field-types use different texture root layouts even when the path looks parallel. Copying a `siteTexture` path into an `expandingIconTexture` slot fails silently in XML, then floods the log at runtime.

Core-only paths that always exist (no DLC required):
- `World/WorldObjects/Expanding/Ambush`
- `World/WorldObjects/Expanding/Camp`
- `World/WorldObjects/Expanding/Caravan`
- `World/WorldObjects/Expanding/DestroyedSettlement`
- `World/WorldObjects/Expanding/JourneyDestination`
- `World/WorldObjects/Expanding/PeaceTalks`
- `World/WorldObjects/Expanding/RoutePlannerWaypoint`
- `World/WorldObjects/Expanding/TravelingShuttle`
- `World/WorldObjects/Expanding/TravelingTransportPods`
- `World/WorldObjects/Expanding/Sites/{Ambush,DownedRefugee,ItemStash,Manhunters,Outpost,PreciousLump,Prisoner,SleepingPawns,Turrets}`

Or just point at a texture you ship yourself (e.g. `UI/YourMod/SomeIcon.png` → `<expandingIconTexture>UI/YourMod/SomeIcon</expandingIconTexture>`). A real shipped PNG works on a stock install with zero DLCs.

*Why it's tricky:* the "Sites" subdirectory under `siteTexture` and the "Sites" subdirectory under `expandingIconTexture` look like the same vanilla namespace but aren't — they're separate path roots. Many specific paths exist in both, but `GenericSite` exists only in `siteTexture`. Copying a vanilla `siteTexture` path into `expandingIconTexture` parses fine; the only signal of failure is the runtime "Could not load Texture2D" error, fired thousands of times per frame because the WorldObjectDef.ExpandingIconTexture getter retries on every render.

## MainButtonDef iconPath must point at a real PNG

A missing MainButton texture fires `Could not load Texture2D at '...'` once per frame (the bottom bar renders every frame), flooding the log to the "max messages" cap in seconds.

Symptom: error like `Could not load Texture2D at 'UI/MainButtons/MyButton' in any active mod or in base resources.` with `×NNNNN` count climbing fast.

Fix: create the PNG at the path the def references. A 64×64 white silhouette on transparent works as a stand-in.

## "Could not load Texture2D/AudioClip" — usual causes

- The def's path has backslashes, a leading slash, an included `.png`/`.ogg` extension, or the wrong casing (case-sensitive filesystems: Linux, macOS).
- The path comes from a DLC and the DLC isn't active.
- The file isn't where the load folder resolves it (`Textures/`/`Sounds/` under a `LoadFolders.xml`-active folder for the running game version).
- The running game loads a different copy of the mod than the one you edited.
- The path is a different field type's root layout (see the `expandingIconTexture` entry above).

## RimWorld auto-loads mod AssetBundles — don't AssetBundle.LoadFromFile them yourself (returns null)

RimWorld's `ModAssetBundlesHandler.ReloadAll` (called during mod load) scans `<mod>/AssetBundles/**` and `AssetBundle.LoadFromFile`s **every extensionless file** it finds (recursively, including platform subfolders like `AssetBundles/StandaloneWindows64/rcgi`). The loaded bundles land in `content.assetBundles.loadedAssetBundles` (a `List<AssetBundle>`, public field on `ModContentPack`).

So if your C# then calls `AssetBundle.LoadFromFile(path)` on that same file, Unity returns **null** — you cannot load one bundle file into memory twice. The symptom looks exactly like a corrupt/version-mismatched bundle even though the file is valid (header reads `UnityFS...2022.3.35f1`).

**Recipe:** retrieve RimWorld's already-loaded copy instead. Iterate `myMod.Content.assetBundles.loadedAssetBundles` and `ab.LoadAsset<T>("AssetName")` (or `ab.LoadAllAssets<ComputeShader>()`) to find your shader/compute/etc. Keep `LoadFromFile` only as a fallback for the (rare) case RimWorld didn't pick it up.

Two related facts from the same code:
- `IsAcceptableExtension` accepts files **with no extension only** — so the bundle file must be named e.g. `rcgi` (no `.bundle`/`.assetbundle` suffix) to be auto-loaded. The `.manifest` sidecar (has an extension) is ignored, which is correct.
- OS targeting uses **name suffixes** `_win` / `_mac` / `_linux` on the bundle filename (`GetBundleNameWithoutOsSpecifier`). A bundle with NO suffix loads on every platform. So for multi-platform shipping, name them `mybundle_win` / `_mac` / `_linux` directly under `AssetBundles/` rather than per-platform subfolders — otherwise RimWorld tries to load all platforms' bundles on every OS (the non-native ones just fail with a logged "Could not load asset bundle").

*Why it's tricky:* nothing tells you RimWorld pre-loaded your bundle; you only see your own `LoadFromFile` returning null and assume the bake/version is wrong. The bundle is fine — you're just loading it a second time.
