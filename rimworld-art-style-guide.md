# RimWorld Art Style Guide (for agent-generated art)

Purpose: a self-contained brief an agent can follow to produce game art (item icons,
buildings, pawns/animals, plants, UI icons, terrain, effects) that fits RimWorld's
vanilla visual style, and that plugs correctly into RimWorld's colouring system.

Read this whole document before generating anything. Then look at the reference
images listed in **§5 Reference files to inspect** — actually open them with the
Read tool — before drawing. Matching the vanilla look by eye is not optional; the
rules below explain *why* vanilla art looks the way it does, but the references are
the ground truth for line weight, palette, and proportion.

Sources this guide is distilled from:
- "Officially unofficial guide to RimWorld's Artstyle" — https://spdskatr.github.io/RWModdingResources/artstyle.html
  (paraphrases Tynan Sylvester's 2017 GDC talk)
- "Colouring in Images" — https://github.com/seraphile/rimshare/wiki/Colouring-in-Images
- Direct inspection of Ludeon's official layered source files at
  `/home/alex/Downloads/Game art source/` (see §5)

---

## 1. Design philosophy — read this first

RimWorld deliberately does not look like a AAA game. The brief from Tynan Sylvester:
**"RimWorld has graphics like a novel has a typeface."** Graphics are a low-key
vehicle for the story the player is generating in their head, not the attraction
itself. Every drawing decision should serve legibility on a busy, zoomed-out colony
screen with hundreds of objects on it at once, not detail for its own sake.

Concretely, art must be:

- **Not ugly.** Clear a minimum bar of pleasantness — that's the whole bar, not a
  pursuit of beauty.
- **Easily identifiable.** A player must recognize what a sprite *is* at a glance,
  at small size, among dozens of other sprites.
- **Low noise.** Simplify aggressively. A field of 40 trees and a pawn walking
  through it must still read clearly — every extra bit of internal detail is a tax
  paid on every single object of that type, everywhere, forever.
- **Ordered by an intensity hierarchy.** Not everything should compete for
  attention equally. Outline weight is the primary tool for this (see §2.4):
  story-relevant things (pawns, items, buildings) get bold black outlines and pop;
  background-ish things (plants) get thin dark-green or no outline and recede, so
  a pawn crossing a plant-choked tile is still the thing your eye lands on.
- **Fast to produce.** Skip detail that doesn't earn its cost. When in doubt, cut
  the extra highlight, extra bevel, extra strand — spend the effort on making the
  silhouette read, not on rendering it.
- **Open to interpretation.** Vanilla art often leaves ambiguity (which side of a
  shelf is "up", exactly what a strange creature's anatomy is) rather than
  over-specifying. Don't feel obliged to resolve every visual question with
  hyper-detail.

## 2. Hard technical rules

### 2.1 Canvas and resolution
- Work on a **square canvas**. RimWorld resizes item textures to **64×64** at
  render time, but the source art is comfortably worked at **128×128** (confirmed:
  vanilla item PSDs such as `Things/Item/Artifact/PsychicAnimalPulser.psd` are
  64×64; many others are authored at 128×128 and downsampled).
- Pawn/animal body textures are typically **128×128** per direction (confirmed via
  `Things/Pawn/Animal/*/*.png`, e.g. `GuineaPig_south.png` is 128×128).
- To make an item read as "smaller" in-game, shrink the drawn subject within the
  canvas (more padding) rather than shrinking the canvas — canvas size maps to
  in-game footprint scale.
- If you want an off-centre composition, add padding to the canvas rather than
  cropping tight.

### 2.2 Outlines
- Vanilla sprites use almost **no interior black outlines/inlines** — separation
  between shapes on the same object is done with shading and colour-value
  contrast, not stroked lines.
- The **silhouette** of "story relevant" things — items, walls, pawns — gets a
  **2–3px solid black border**.
- **Plants** get a thin **dark-green border, or no border at all**, which is the
  main mechanism that makes them recede visually behind pawns/items (see §1).
- Confirmed by inspection: `Things/Building/Furniture/Couch_south.png` has a bold
  ~2–3px black outline; a flattened render of
  `Things/Plant/BerryPlant/BerryBushA.psd` has no black outline at all, just
  value contrast between foliage clusters.

### 2.3 Detail floor
- Don't draw detail thinner than **~3px** unless it has strong colour/value
  contrast — anything finer just turns to noise at gameplay zoom and render
  resolution.
- Favor a handful of large, clear value/colour shapes over many small ones.

### 2.4 Intensity hierarchy in practice
When placing a new sprite type, decide deliberately where it sits:
- Pawns > items/buildings > plants/terrain decoration, in both outline weight and
  internal contrast. If unsure, undersell rather than oversell — err toward less
  contrast/detail for background-ish objects.

### 2.5 Background trick for stray white pixels
- Vanilla source files often include an extra **fully-black layer at ~1% opacity**
  behind everything else. This doesn't affect the visible result but prevents
  stray fully-transparent-but-white RGB pixels (common artifact of some
  export/anti-aliasing paths) from showing as white fringing in-game. Reproduce
  this if your export pipeline shows white fringes on transparent edges; otherwise
  it's not necessary — the underlying bug it works around depends on your
  tooling's alpha handling.

## 3. Colouring system — how RimWorld tints art (mandatory to get right)

RimWorld does **not** ship a separately-drawn sprite per material/colour variant
for most things. Instead:

1. **Draw the base texture mostly greyscale / desaturated**, using value (light↔
   dark) to carve out form. This is the layer the game shows.
2. RimWorld overlays a solid **fill colour** on top using the **Multiply** blend
   mode — exactly like flat colour laid over black-line-art in a colouring book.
   `<color>(140,74,41)</color>` in the ThingDef XML is that fill colour.
3. Multiply-over-grey/white recolours cleanly. Multiply-over-a-different-colour
   muddies (e.g. red multiplied over green tends toward brown/black) — so **don't
   put strong local colour under areas meant to be recolourable**; keep those
   areas neutral/grey-valued.
4. Areas that should **never** be recoloured (eyes, a white belly patch, metal
   trim, etc.) are protected using a second file: an **alpha mask**
   (`shaderType: CutoutComplex`). The mask is a same-dimensions image where the
   mask's own colour (commonly drawn in red, but only alpha matters at runtime)
   marks which pixels the coloured fill layer is allowed to touch — mask pixels
   present ⇒ fill applies there; masked-out/transparent ⇒ base texture shows
   through unrecoloured.
5. Mask file naming: **identical name plus a trailing `m`**, same folder, same
   pixel dimensions as the base texture. E.g. `Pig_side.png` → `Pig_sidem.png`.
   RimWorld auto-discovers `<texPath>_north/_south/_east` plus their `...m`
   counterparts when `shaderType` is `CutoutComplex`. If a thing needs no masked
   region, use `shaderType: CutoutSkin` instead and skip the mask file entirely.
6. This mask mechanism is **only available for `ThingDef`s and `race`s** (pawns,
   animals, items, buildings) — not for arbitrary UI art, world tiles, etc.,
   which are just flat pre-coloured images.

Confirmed by inspection: `Things/Pawn/Animal/GuineaPig/GuineaPig_south.png` +
`GuineaPig_southm.png` — the base texture is a soft grey/pink guinea pig; the mask
is a mostly-black image with red patches that exactly cover the body fur (the
recolourable region) while leaving the pink nose and the eye area outside the
mask, so those never take the fill tint.

**Practical workflow implication:** when a thing is meant to be recolourable
(most items, animals, furniture, apparel), always produce the base texture *and*
a matching alpha mask, not just one flat image. When it's not meant to be
recolourable (most UI icons, world/terrain art, effects/motes), a single flat
image is correct and no mask is needed.

## 4. Preferred production format

**Prefer SVG** wherever the subject is naturally vector-shaped (icons, weapons,
furniture, most items, UI elements, simple pawns/animals, terrain motifs). Build
it as clean layered/grouped vector shapes — this keeps outlines crisp at any
export resolution, makes iterating on colour/shape trivial, and makes producing a
matching alpha-mask layer straightforward (duplicate the recolourable groups,
fill solid, export separately). Rasterize the final SVG to PNG at the target
resolution (64×64 or 128×128 per §2.1) for use in-game, using a real vector
renderer (e.g. `rsvg-convert`, `magick`, Inkscape CLI) — check the exported PNG
for anti-aliasing fringes per §2.5.

**When a subject genuinely needs painted/raster technique** (organic creature
fur/skin shading, terrain textures, weather/gas effects, painterly world art),
use a **layered, non-proprietary raster format** — GIMP's **XCF** — instead of a
flattened PNG as your working file, and export flattened PNG(s) for the game.
Keep the base-texture layer(s) and the alpha-mask shapes in the same XCF as
separate layers/layer groups so the two exports (`Name.png`, `Namem.png`) stay in
sync as you iterate. Do not produce PSD; it's proprietary and unnecessary — XCF
covers the same layered-raster need and is fully open.

Either way, never hand-author a flattened raster file as the *only* copy of
non-trivial art — keep the editable source (SVG or XCF) alongside the exported
PNG(s) so future recolours/edits don't require redrawing from scratch.

## 4a. Implying height on a flat-topped building (box shading)

Most buildings read as sitting on the ground with real height, even though
the base texture is a flat top-down/three-quarter sprite. Vanilla does this
with **two flat, hard-edged colour planes**, not a gradient:

- A **top/frame plane** (lighter) occupies the large majority of the sprite's
  height — confirmed by cropping `Things/Building/Power/Battery_east.psd` at
  high zoom: the lighter steel-blue frame colour runs for roughly the top
  80% of the object's height.
- A **front-face plane** (the object's vertical wall, facing the viewer) is a
  separate, noticeably darker flat colour occupying roughly the bottom
  15–20%, with a **crisp, hard-edged line** between the two planes — no
  blur, no gradient. Confirmed on the same Battery crop: the transition from
  `#606C7A`-ish frame to a near-black `#26282B`-ish band is a single sharp
  pixel row.
- Get the proportion and contrast right: a 50/50 split, or a front-face plane
  that's only slightly darker than the top plane, does not read as "this
  object has height" — it just reads as arbitrary flat colour blocking (this
  was tried and rejected during PowerDiode's building art pass). The front
  face needs to be a *small* band and a *strong* value jump to sell the
  illusion.
- **Gradients are reserved for genuinely curved/cylindrical surfaces** — e.g.
  `Things/Building/Power/GeothermalPlant.psd` uses a soft blurred transition
  specifically at its beveled/curved corners, confirmed by sampling pixel
  values across that transition. A flat-sided box (most furniture, most 1x1
  and 2x2 utility buildings) should use the hard-edged two-plane technique
  above instead, not copy the geothermal plant's blur.
- This stacks with §3's recolour system: keep the "protected"/never-recolour
  regions above (see §5's `Battery_east.psd` panels for how a warm tan
  recolourable-looking panel sits inside a cooler structural frame — vanilla
  power buildings mix a cool steel blue-grey frame with a warm tan/khaki
  panel insert; using only one of the two hues reads as flat/off, so when
  compositing a frame + panel design, deliberately pull from both families).

## 4b. Avoiding accidental pareidolia in small functional icons

When a building's face carries a small pictogram/icon to distinguish its
function (a switch's toggle glyph, a valve's flow direction, etc.), watch out
for accidentally drawing a face:

- **Two similar round/oval shapes side by side, with one more circular
  element centred below them, reads as eyes-and-mouth** almost regardless of
  what those shapes actually represent — this was hit directly when two
  prong/slot rectangles were placed above a centred circular status lamp;
  players immediately read it as a face wearing a pacifier.
- Prefer a **single, self-contained glyph** (one silhouette that already
  reads as the intended object — e.g. a plug drawn as one shape with prongs
  merged into its body, not prongs and body as separate floating pieces) over
  assembling the icon from multiple separately-legible parts.
- If the design also needs a status/power indicator lamp, put it somewhere
  physically separate from the identifying glyph (e.g. a corner of the
  outer frame rather than the centre of the same panel) so the two don't
  compose into an unintended face.
- Ambiguous abstract shapes (e.g. plain prong-shaped rectangles with no
  surrounding context) can read as "random tubes" rather than the intended
  object — when the icon is meant to depict a real object (a plug, a valve),
  draw enough of that object's recognisable silhouette (the plug's body plus
  prongs, together, as one piece) rather than an abstracted fragment of it.
- Big, glossy, saturated icon glyphs (e.g. a large single-colour arrow filling
  most of the face) read as "cartoony" and out of place *when painted onto an
  in-world object's face* — vanilla's own small pictograms painted onto
  buildings (see `UI/Icons/` in §5) are compact, a small fraction of the
  object's total face, and desaturated/metal-toned rather than bright flat
  colour fills. This does **not** apply to `Command` gizmo icons — see §4d.

## 4d. Command gizmo icons (the bottom-of-screen action buttons) are a different register

The small square icons that appear in the gizmo row at the bottom of the
screen when something is selected (`UI/Commands/`) follow different rules
than a pictogram painted onto a building's face (§4b) or an in-world item
icon (§2.2–§2.3):

- They use **bright, saturated flat fill colours** (not desaturated/metal-
  toned) **with a bold black outline** — confirmed by direct inspection of
  `UI/Commands/DesirePower.png` and `UI/Commands/TryReconnect.png`. Don't
  extrapolate the muted-palette guidance for in-world functional pictograms
  (§4b) onto these; they're a different visual register with a different
  purpose (a clickable button that must pop against the bottom UI bar, not
  a detail integrated into a building's rendered face).
- At the 64×64 canvas / ~32-40px on-screen size these are actually shown at,
  a **3px outline reads as bold and clean; 4px starts to read as thick/heavy**
  once anti-aliased down — prefer 3px for the outer silhouette stroke of a
  Command icon, reserving thinner (~2px) strokes for internal
  detail/segmentation lines that shouldn't compete with the outer outline.
- When a glyph needs multiple visually-adjacent parts (e.g. an arrowhead
  feeding into a body shape), merge the parts that touch or overlap into a
  **single polygon** rather than drawing them as separate stroked shapes
  placed next to each other. Two separate stroked shapes that touch or
  overlap produce a double-stroke seam at the shared edge that reads as a
  rendering glitch, not a design choice — even though this is a variant of
  the same "single self-contained glyph" principle as §4b, it's worth
  calling out separately because the failure mode here is a visual artifact
  (crossed/doubled outlines) rather than pareidolia.
- A glyph meant to read as a specific real-world symbol (e.g. an electronic
  diode's triangle-and-bar) benefits from a couple of short straight lead
  lines extending from either side of the main shape, matching that symbol's
  conventional schematic form — without them it can easily be misread as an
  unrelated glyph (a bare triangle-and-bar read as a media player's "next
  track" button rather than a diode).

## 4c. Verify palette and proportion claims against real source pixels

Don't assert "vanilla uses colour X" or "the band is Y% of the height" from
memory or by eye alone — sample the actual reference file:

```
magick "Some/File.psd[0]" /tmp/preview.png         # flatten a PSD to PNG
magick preview.png -crop WxH+X+Y +repage crop.png  # zoom into a region
magick preview.png -crop 1x1+X+Y +repage txt:-     # read one pixel's exact colour
```

Guessed palettes and proportions were called out as "off" more than once
during PowerDiode's building art pass before this workflow was adopted —
treat it as the default way to answer any "what colour/proportion does
vanilla use here" question, not a fallback for when eyeballing fails.

## 5. Reference files to inspect

Root of Ludeon's official layered source art (organized by DLC): 
`/home/alex/Downloads/Game art source/`

Most files are `.psd`. Many are also already available as flat `.png` — prefer
those directly. For a `.psd` you need to look at, flatten it to PNG first:

```
magick "path/to/File.psd[0]" /tmp/preview.png
```

(`[0]` = the first/composite layer; drop it to get a per-layer index list via
`magick identify "path/to/File.psd"`.)

Specific files worth opening (already inspected while building this guide —
descriptions below reflect what's actually in them):

| Path | Why look at it |
|---|---|
| `Game art source - RimWorld/Things/Pawn/Animal/GuineaPig/GuineaPig_south.png` | Base pawn/animal texture: soft grey/pink values, no black outline, small dark eye for form. |
| `Game art source - RimWorld/Things/Pawn/Animal/GuineaPig/GuineaPig_southm.png` | Matching alpha mask for the above — red = recolourable fur, black = protected (nose/eye). Study this pair together to understand §3. |
| `Game art source - RimWorld/Things/Building/Furniture/Couch_south.png` | Textbook "story relevant" outline treatment: bold ~2–3px black silhouette border, flat interior shading, no internal outlines. |
| `Game art source - RimWorld/Things/Plant/BerryPlant/BerryBushA.psd` (flatten with `magick` first) | Plant treatment: no black border at all, just dark-green/blue value clusters — compare directly against the couch above to feel the intensity-hierarchy contrast from §1/§2.4. |
| `Game art source - RimWorld/Things/Item/Equipment/WeaponRanged/AssaultRifle.png` | Clean small (~128px) item icon: bold outline, minimal internal detail, flat mid-tone shading blocks — good baseline for any hand-held item/weapon. |
| `Game art source - RimWorld/Things/Item/Equipment/WeaponMelee/` (any file) | More item-icon examples at a different silhouette shape. |
| `Game art source - RimWorld/UI/Commands/` and `UI/Icons/` | Flat, single-colour-ish UI iconography — no mask system here, just clear high-contrast pictograms; look at several for the UI-icon register (as opposed to the in-world-object register above). |
| `Game art source - RimWorld/Things/Item/Dye/Dye_a.png` (and `_b`, `_c`) | Small, simple flat item icon — good minimal reference for how little detail a legible small icon actually needs. |
| `Game art source - RimWorld/Things/Item/Artifact/PsychicAnimalPulser.psd` | Confirms the 64×64 authored-canvas convention for small item icons (`magick identify` on it reports 64×64). |
| `Game art source - RimWorld/Things/Building/Power/Battery_east.psd` | Textbook flat-box height shading (§4a): crop/zoom it and sample pixels — a light steel-blue frame for ~80% of the height, then a hard-edged, near-black front-face band for the bottom ~15–20%, plus a warm tan panel insert. |
| `Game art source - RimWorld/Things/Building/Power/PowerSwitch.psd` | Small, compact power-building silhouette with the same steel-blue-frame/warm-accent palette; also a good reference for how restrained/small a functional icon should be relative to the whole face (§4b). |
| `Game art source - RimWorld/Things/Building/Power/GeothermalPlant.psd` | Contrasts with Battery above: this one has genuinely curved/bevelled corners, and *that* is where a soft gradient transition is used — sample pixel values across the transition to see it's a real blur, unlike Battery's hard edge. Don't copy this file's gradient technique onto a flat-sided box. |

Browse sideways from any of these — sibling files in the same folder (e.g. other
animals under `Things/Pawn/Animal/*/`, other furniture under
`Things/Building/Furniture/`) are equally valid references for the same category.

Community tutorials referenced by the sources above (for animal/creature
painting workflow specifically): Ekksu's RimWorld-style sprite tutorial —
linked from https://spdskatr.github.io/RWModdingResources/artstyle.html — walks
through a full paint-over workflow with example screenshots.

## 6. Pre-flight checklist for the agent

Before generating a new sprite:
1. Identify its category (pawn/animal, item, building, plant, terrain, UI icon,
   effect/mote) — this decides the outline treatment (§2.2) and whether it needs
   an alpha mask (§3).
2. Open at least one same-category reference from §5 and look at it, not just
   read about it.
3. Decide the canvas size (§2.1) and whether the source will be SVG or XCF (§4).
4. Draw mostly in value (light/dark), keep colour muted/neutral under any
   region meant to be recolourable, and stay above the ~3px detail floor (§2.3).
5. If the thing is a ThingDef/race and should be recolourable, produce the base
   texture and a same-size, correctly-named `...m` alpha mask together, not as an
   afterthought.
6. If the thing is a flat-topped building/prop that should read as having
   height, use the hard-edged two-plane technique in §4a — sample a real
   reference file's pixels first rather than guessing the band proportion or
   contrast.
7. If the thing carries a small functional icon/pictogram, sanity-check it
   against §4b's pareidolia trap (two round shapes over a centred circle) and
   keep it a single self-contained glyph, small relative to the whole face.
8. Export flattened PNG(s) at the target resolution; keep the SVG/XCF source.
9. Compare the result side-by-side against the reference image(s) from §5 before
   calling it done — check outline weight, detail density, and colour value
   consistency specifically. For colour/proportion claims specifically, verify
   with real pixel samples (§4c) rather than eyeballing.
