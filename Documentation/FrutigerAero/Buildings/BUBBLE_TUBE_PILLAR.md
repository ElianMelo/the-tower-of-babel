# Bubble Tube pillar

**Status:** accepted by the user on 2026-10-30 as the initial version ("great initial version"). This is the baseline for future pillar iterations. It replaces the white continuous support with aqua collars, which is now history.

Selected from the ten Round03 proposals (design 04). It replaces the pillar's completed mesh, construction stages, incoming chunks and material. The following are unchanged:

- the prefab GUID, root transform and capsule collider (r 0.41, h 4)
- the `Building` settings and the `PillarConstructionMotion` component
- the completed mesh's ±0.41 m × 4 m bounds, so `TowerGenerator.PillarSupportRadius` and the deck supports do not move

## Iteration history

| Step | What changed | Why |
| --- | --- | --- |
| Round03 proposals | Ten Blender designs, all pearl and aqua, all inside the pillar footprint: Aquarium Core, Helix Flute, Pearl Bead Stack, **Bubble Tube**, Hourglass Glow, Tri-Strand Spiral, Halo Rings, Scallop Shell, Aero Ionic and Wave Fins. One review render. | The user asked for ten replacement pillars that keep the Frutiger Aero look and the pillar's role. |
| v1 game asset | A real glass and water tube was not usable, because pillars render as one instanced opaque draw. Instead: one solid 860-triangle lathe with an opaque shader that traces the water, spine and bubbles. 11 stages, 80 chunks, and per-instance bubble amount and motion. | The user selected design 04 and asked for prefab replacement, all stages, and per-instance bubbles and water motion. |
| v1 tuning | The proposal mesh was 3,196 triangles. Fillets were removed and the lathe uses 24 sides, giving 860. Ray wobble was halved because it turned the spine into a squiggle. `_InstanceVariation` = 0 now also zeroes phase and swirl. | Triangle budget near the old 764. Readability. A testable "identical" mode. |
| v2 bubbles (accepted) | The bubbles were too dense and uniform. Changes: refraction through hand-blown glass (air to glass to water); iridescent thin-film rims and highlights; varied transparency, size and shape; a large-bubble shell; streams and calm spells over time; fewer bubbles by default. | User feedback with three bubble references; glass distortion was marked most important. |

Mistakes worth remembering:

- **Inner glass normal:** an early v2 build offset the position used for the inner-wall normal, so the normal pointed sideways and hid the spine. `BtGlassNormal` now takes a separate `pattern` argument, so callers vary the waviness without moving the radial direction.
- **Clipped bubbles:** large wobbling bubbles were clipped into crescents at cell edges. The wobble is now capped (`min(..., 1.8)`) and centres keep a `1 - 2.7 * size` margin.

## Model

- Pearl base and cap, a blue enamel band, a chrome collar, and a glass tube from y 0.485 to 3.515 (radius 0.362). These constants are duplicated in `BuildingBubbleTube.hlsl` (`BT_Y0`, `BT_Y1`, `BT_GLASS_R`, `BT_WATER_R`); change both together.
- One solid, single-submesh lathe: **860 triangles**. UV1.x encodes the surface region as `(id + .5) / 8`: 0 pearl, 1 enamel, 2 chrome, 3 glow (blueprint guides), 4 water tube. Adding a region needs a new id and a branch in the forward pass.
- **Stages:** 0 is a glowing blueprint, 1–9 add eight seeded Voronoi chunks each (seed 712809), and 10 is the completed model.
  - Stage meshes range from 1,082 to 5,648 triangles. All stages and the 80 chunks are closed.
  - Chunk cut faces inside the tube use the water region, so partial stages show water in cross-section.
- **Source:** `Tools/BuildingAero/build_bubble_tube_pillar.py` (headless Blender), producing `ArtSource/Buildings/FrutigerAero/BubbleTube/BubbleTubePillar.blend` and `BubbleTubePillarReport.json`.
- **Unity assets:** `Models/Buildings/FrutigerAero/BubbleTubePillar.fbx`, `Construction/BubbleTubeStages.fbx`, `Construction/BubbleTubeChunks.fbx` and `Construction/BubbleTubeChunks.asset`. The earlier `Pillar.fbx`, `PillarStages.fbx` and `PillarChunks*` remain as history and are unreferenced.

## How the shader works

The shader is `TowerOfBabel/Buildings/Bubble Tube Pillar`, in `Shaders/BuildingBubbleTubePillar.shader` (passes and material) and `Shaders/BuildingBubbleTube.hlsl` (interior trace). The material is `Models/Buildings/FrutigerAero/Aero_BubbleTube_Pillar.mat`.

1. **Regions:** the vertex shader reads the region from UV1. Pearl, enamel and chrome use URP PBR with the shared Aero finish reflection and thin film. Pearl reuses the marble map and normal map from `Aero_WhiteMarble_Pillar`.
2. **Per-instance seed:** taken from the object origin (`originSeed.w`), so no per-instance data or property blocks are needed. `BubbleTubeVariation` derives:
   - the bubble amount, skewed toward few (`a * a`)
   - the water motion
   - the phase and swirl direction
3. **Glass and refraction** (water region): the ray refracts at the outer wall with IOR 1.5, using a wavy per-instance normal (`BtGlassNormal`). It crosses the glass, then refracts into the water with IOR 1.5 → 1.33 at a wavy inner wall that water ripples also move. Cut faces inside the tube refract directly into the water.
4. **Water:** the ray traces against the water cylinder, the tube ends and the pearl spine. Water absorption depends on path length (`_WaterDensity`). Caustics fall on the spine and back wall, and collar lamps light the water from both ends.
5. **Bubbles:**
   - Bubbles sit on four cylindrical shells: one sparse shell of large bubbles (cell 0.30 m), then three small-bubble shells from far to near (cell 0.11 m). Only the front crossing of each shell is shaded.
   - Each cell's bubble gets its size, shape wobble, stretch, thin-film hue, transparency and highlights from hashes.
   - The bubble count is not a fixed density. `BubbleTubeBurst` evaluates it at the moment the bubble left the floor, which creates rising streams and calm spells.
   - Because bubbles are sampled along the bent ray, the glass and water ripples distort them.
6. **Distance:** bubbles and caustics fade out by `_WaterDetailDistance`, leaving plain absorbing water.
7. **Contracts kept from Aquatic Finish:**
   - the incoming-chunk dissolve (`_BuildMode`, `_BuildReveal`, `_BuildClock`)
   - reflection-capture clipping (`AeroClipReflection`)
   - wetness on non-water regions
   - shadow, depth and depth-normal passes
   - GPU instancing

## Tuning guide

The table shows the current accepted values on the material. `BubbleTubePillarAuthoring.Apply()` writes the bubble values, so change them there as well as on the material.

| Property | Value | Effect |
| --- | --- | --- |
| `_BubbleAmountMin` / `_BubbleAmountMax` | 0.06 / 0.45 | Range of bubble-cell occupancy per pillar; most pillars sit near the minimum. |
| `_BubbleBurstiness` | 0.85 | 0 gives a steady flow. Higher values give calm spells (0.2×) and streams (2.4×). |
| `_BigBubbleChance` | 0.45 | How often the large, wobbling bubbles appear. |
| `_BubbleIridescence` | 1.2 | Strength of the thin-film rim, swirl and highlights. |
| `_BubbleSpeed` | 0.22 m/s | Rise speed; each column varies it by 0.6–1.4×. |
| `_GlassDistortion` | 1 | Waviness of the hand-blown glass; 0 is perfect glass. |
| `_WaterMotionMin` / `_WaterMotionMax` | 0.15 / 1.25 | Range of ripples, caustic speed, swirl and bubble wobble per pillar. |
| `_InstanceVariation` | 1 | 0 makes every pillar identical; the tests use this. |
| `_WaterDensity` | 3.2 | Water absorption; higher values hide the spine more. |
| `_GlowStrength` | 2.5 | Collar lamps and blueprint guides. |
| `_WaterDetailDistance` | 85 m | Distance at which bubbles and caustics fade out. |

## Contracts for future iterations

- Keep the pillar **opaque, single-submesh and instanced**, using one material. The near pool and far batches copy the prefab material and draw `GetStageMesh(stage)` submesh 0.
- Keep the completed mesh's bounds at ±0.41 m × 4 m with the pivot at the base. Deck support layout derives from these bounds.
- Keep chunk names as `Pillar_Chunk_<stage>_<index>`. The library parses the stage from the name.
- Keep the interior seeded from the object origin. Moving a pillar changes its look, which is intended.
- Rebuild order:
  1. Run `build_bubble_tube_pillar.py` in Blender.
  2. Run `BubbleTubePillarAuthoring.Apply()`.
  3. Run `BubbleTubePillarPreview.Capture(...)`.
  4. Run `BubbleTubePillarEditModeTests`.
  5. Run PlayMode tests last, through the test runner only.

## Known limitations and ideas for later

- **Not yet reviewed in context:** the pillar hasn't been seen in the live Tower scene. Check it against the dynamic interior rays, night lighting, wet weather and bloom. Chrome reads as flat grey in the preview scene because there is no reflection probe there.
- **Performance unprofiled:** the interior costs about four shell tests and two refractions per water pixel. There is no GPU profiling on target hardware yet. If needed, reduce the shells at mid distance or lower `_WaterDetailDistance`.
- **Bubble rendering:**
  - Bubbles are discs on shells, not spheres, and do not overlap or merge.
  - A bubble behind the spine can be partly hidden at the spine's silhouette edge.
  - The large bubbles could gain more stretch and deformation, as in the second reference.
- **Possible additions:** chromatic dispersion at the glass edges; bubbles collecting under the top collar; an interaction or proximity response (stirring when the player builds nearby).
- **Rendering paths:** shadows are cast as a solid cylinder, and planar reflection captures see the opaque tube.
- **Unrelated test failures:** the same failures were seen in both runs: 12 in EditMode, and 4 UI, font and cookie failures in PlayMode. They were not compared against a baseline. The full test runs also overwrite tracked preview images and `Lighting/DynamicRays/Performance.json` in `Documentation/`; discard those before committing.

## Authoring, review and references

- `Editor/BubbleTubePillarAuthoring.Apply()` (menu: Tower of Babel ▸ Buildings ▸ Apply bubble tube pillar) imports the three FBX files, builds the chunk library and material, rewires the prefab, and writes `BubbleTubePillarInspection.json`.
- `Editor/BubbleTubePillarPreview.Capture(stages, motionTime, suffix, frames, frameStep, closeUp)` renders Edit Mode review images to `_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/`:
  - [stages](../../../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-Stages.png)
  - [per-instance variation](../../../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-Instances.png)
  - the [same pillars 34 s later](../../../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-Instances-Later.png)
  - a [glass close-up](../../../_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/BubbleTube-CloseUp.png)
- Proposal render: the [Round03 lineup](../../../_Screenshots-and-previews-to-delete/PillarDesigns/Round03/Pillar-Designs-Lineup.png), from `Tools/BuildingAero/build_pillar_designs.py`. Its `.blend` and FBX files are in `ArtSource/Buildings/FrutigerAero/PillarDesigns/Round03/`.
- Bubble references: `Frutiger Reference/Asset/Bubble-Iridescent-Single-User.png`, `Bubble-Large-Distorted-User.webp` and `Bubble-Cluster-Glass-Distortion-User.webp`, listed in the [reference index](../../../Frutiger%20Reference/INDEX.md).

## Validation (2026-10-30)

- `BubbleTubePillarEditModeTests`: 11 of 11 passing. They check:
  - the material, instancing and absence of shader errors
  - eleven distinct single-submesh stages
  - bounds, collider and triangle budget
  - region encoding
  - eight chunks per stage
- The same suite also renders the tube and confirms that:
  - pillars at different origins look different only when variation is on
  - the bubble amount changes the bubble count
  - water motion changes the frame-to-frame movement
  - bubbles come in streams and calm spells within one pillar
  - the glass bends what is behind it
  - the bubbles show thin-film colour
- Full EditMode suite (v1): 220 of 232 passing. The 12 failures are unrelated: sun flare, anisotropy, fonts, the floor-tile collider, the island asset count and projection-light cookies.
- PlayMode, run through the test runner after v1 and again after v2: 58 of 62 passing both times, including tower build, instant build and floor progression. The 4 failures are UI hover, click and font checks and a glass-floor projection cookie.
