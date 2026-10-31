# Aurora Arch

The arch prefab uses the **Aurora Curtain** design (Round05 design 10), chosen by the user on 2026-10-31. It is a glossy bright white arch with a round opening. An animated aurora curtain glows across its upper wall and runs continuously through every arch on a floor.

## How it was chosen

| Round | Direction | Outcome |
|---|---|---|
| 01 | Decorated spandrels | Rejected: "try 10 different ideas" |
| 02 | Form and material experiments | Rejected: asked for marble and stone with normal-map depth |
| 03 | Carved marble | 05 Wave Scroll used as the base for the next round |
| 04 | Wave Scroll Aero variations | Rejected as a direction: "return to glossy bright white" with detail like [the light ribbon reference](../../../Frutiger%20Reference/Asset/Arch-Light-Ribbons-User.png) |
| 05 | Glossy white with luminous strand ribbons | **10 Aurora Curtain** picked |

Feedback on the pick, all applied:

- **Remove the line near the arch.** The rounded roll along the opening edge is gone; the intrados is a clean edge.
- **Make the curve more round.** The opening was a tall half-ellipse (rise 1.0 m over a 0.75 m half-width). It is now a true semicircle (radius 0.747 m) on short straight legs, with the crown kept at the same height.
- **A shader that mimics real aurora behaviour.** The baked emission texture is replaced by the animated `TowerOfBabel/Buildings/Aurora Arch` shader described below.
- **Make the inner part more round** (second pass, same day). The opening edge is now a rounded rim: on both faces a quarter-round of radius 0.10 m curls into the opening, with a flat strip in the middle of the 0.4 m thickness, so the opening reads as a soft, pillowy edge rather than a square cut. The half-circle shape and the fit are unchanged.
- **Soft, wavy base and green** (third pass, same day, with the [aurora reference](../../../Frutiger%20Reference/Asset/Aurora-Colours-User.png)). The flat bright base line was replaced by a soft, swaying edge. Green was added, and the colours now blend into each other along and up the curtain.
- **The lines go beyond the base line** (fourth pass, same day). Feedback: "I can still see the base line". The rays now hang below the curtain to different lengths and fade out, and the horizontal glow band was removed.
- **More subtle, with white space** (fifth pass, same day). The curtain no longer covers the whole wall. It appears in patches that drift around the ring, with blank stretches of plain white between them. Each ray is broken into segments by soft, upward-stretched white gaps that drift slowly up. Overall brightness was lowered (`_AuroraStrength` 1.3, `_AuroraTint` 0.6).
- **50% smaller, 20% more blank space** (sixth pass, same day). The curtain is half as tall (`_AuroraHeight` 0.5 scales ray reach, hanging tips and colour bands), sits higher on the wall (`_AuroraBase` 1.7), and has gentler edge waves (`_AuroraEdgeWander` 0.12). Coverage dropped from 0.5 to 0.3. The coverage mapping was also corrected so the value is roughly the lit share of the wall (the first attempt at 0.3 left only specks).
- **Random line lengths** (seventh pass, same day). Feedback: "the lines are almost the same length". Each ray now has its own random top (from about 0.1 to 1.25 of the curtain height) and its own random hanging length, eased softly into its neighbours. Short stubs sit beside long streaks.
- **Slower, and a lower curve** (eighth pass, same day, with a red-line sketch on an in-game screenshot). The aurora moves at half speed (`_AuroraSpeed` 0.5). The opening no longer has straight legs and a tall semicircle: it is a low, round half-ellipse that starts right on the pillar tops (crown at Y 0.76 instead of 1.30). The rim, fit and aurora are unchanged.
The proposal scripts and renders for Rounds 01–05 are in `Tools/BuildingAero/build_arch_designs*.py` and `ArtSource/Buildings/FrutigerAero/ArchDesigns/`.

## Fit and the two old bugs

The user reported that arches overlapped each other and did not line up with the pillars. Both are fixed.

- **Overlap.** The old arch ran 2.157 m past its start axis, but bays are 2.0944 m apart. The new arch runs exactly from the start pillar's axis (local X 0) to the next one's (X 2.0944, at the 96 m base ring). Only a half-round cap of radius 0.2 m sits behind X 0. It fills the small wedge that opens at each joint because of the tower's curve.
- **Seam duplicate.** `TowerGenerator.GenerateTower` stepped a float angle up to 2π, and on 21 of 45 floors this added an extra pillar and arch on top of the first ones. The generator now places a whole number of bays per ring (`PillarsPerRing`, 288 at the base). It also stretches each arch along X to that ring's exact chord (`ArchChordScale`); the stretch is always under 1%. `TowerGenerator.RingLayout(level)` is the single source for this layout.

Other dimensions:

- Thickness ±0.2 m, feet at Y 0.14, flat top at Y 2.15 (prefab placed at `yOffset` 3.85, so the feet sit on the 4 m pillar tops).
- The opening starts 0.30 m from each pillar axis (inside the pillar's 0.41 m radius). It rises straight from the feet as a low, round half-ellipse (0.747 m half-width, 0.62 m rise) to the crown at Y 0.76, with no straight legs.
- The rounded rim (`RIM` 0.10 m, five steps per quarter-round) leaves the flat front face starting 0.10 m outside the opening. The aurora mask starts beyond it.
- These constants are duplicated in `BuildingAuroraCurtain.hlsl` (`AURORA_ARCH_*`, including `AURORA_ARCH_RIM`); change both together.

## The aurora shader

`Assets/_Project/Shaders/BuildingAuroraArch.shader` handles the surface. `BuildingAuroraCurtain.hlsl` computes the curtain from object-space height and the world angle around the tower. It uses no textures. The curtain models these features of a real aurora:

| Behaviour | How it is drawn | Material control |
|---|---|---|
| No base line: rays hang below the curtain | Each ray hangs below the curtain base by its own length (up to about 0.4 m) and fades out toward its tip, so the bottom is a ragged fringe of streaks. Each ray has one length (not blended with its neighbours), so the tips do not form V-shaped points. The veil above fades in slowly | `_AuroraBase` |
| Lower edge sways like a ribbon | Three wave layers (about 2.6 m, 1.1 m and 0.4 m long) travel along the curtain and lift and drop the base by up to about 0.3 m | `_AuroraEdgeWander` |
| Soft veils carrying the rays | Broad luminous sheets brighten and dim along the curtain; the rays are streaks inside them, not separate bars | (built in) |
| Patchy curtain with white space | Large patches drift around the ring; between them the wall stays plain white (zero aurora). Rays are broken by soft white gaps that drift upward | `_AuroraCoverage` (roughly the lit share of the wall, 0.3), `_AuroraHeight` (curtain size, 0.5) |
| Vertical rays along field lines | Two scales of ray noise in the curtain's frame; each ray reaches its own height | `_AuroraRayDensity` |
| Rays slide along the curtain and flicker | Rays drift sideways; their brightness changes over time | `_AuroraRayDrift`, `_AuroraFlicker` |
| Curtain folds, brighter where the sheet bends back | Slow sideways warp; brightness rises where the warp compresses | `_AuroraFolds` |
| Bright surges travelling along the curtain | A moving brightness wave | `_AuroraSurge` |
| Calm and active spells (substorms) | Slow activity cycle, offset per floor | `_AuroraActivityMin`, `_AuroraActivityMax` |
| Colours that blend into each other | Green base, teal body, blue upper part and violet-pink top, blended continuously. Slow drifting patches shift each section greener or more violet, and pink tinges appear on the lower edge in places | `_AuroraLowColor`, `_AuroraMidColor`, `_AuroraHighColor`, `_AuroraFringeColor` |

The colours follow the user's [aurora reference](../../../Frutiger%20Reference/Asset/Aurora-Colours-User.png) (2026-10-31): green, teal, blue, violet and pink blending into each other. Green is allowed here at the user's request, as an exception to the building kit's white-and-blue palette. Very bright folds roll off softly, so they stay coloured instead of clipping to white. The live controls are in `Aero_AuroraArch.mat`.

- **Continuous curtain.** The horizontal coordinate is the angle around `_TowerCenter`, so one curtain runs around a whole floor and through every arch. Every noise layer has a whole number of cells per revolution, so there is no seam where the angle wraps. The joint caps also carry the curtain, so no dark line shows at the pillars.
- **Day and night.** `_AeroDaylight` drives the balance. At night the curtain glows (`_AuroraNightBoost`). By day it mostly colours the white glaze (`_AuroraTint`), and its glow drops to 45%, so it still reads in bright light.
- **Distance.** Rays fade to their average once they are smaller than a pixel, which stops shimmering on far arches.
- **Preview clock.** `_MotionTime` freezes the animation for captures; `-1` uses engine time.
- **Unchanged.** Construction dissolve, wetness, reflection clipping, shadow, depth and depth-normal passes match the other building shaders. It is opaque, single-submesh and GPU-instanced, so the near pool and far batches render it unchanged.

## Assets and regeneration

- `Tools/BuildingAero/build_aurora_arch.py`: run headless with Blender 5.2 (`blender --background --factory-startup --python ...`). It builds:
  - the completed arch: 1,088 triangles (the old arch had 1,680), closed, with the rim and the cap built into the mesh;
  - blueprint stage 0;
  - 80 Voronoi chunks (seed 431907, eight per step) and stages 1–9.
- UV1 `PaletteUV` holds the region: 0 glossy white, 1 aurora face, 3 blueprint glow.
- Exports: `AuroraArch.fbx`, `Construction/AuroraArchStages.fbx`, `Construction/AuroraArchChunks.fbx`. The editable scene is `ArtSource/Buildings/FrutigerAero/AuroraArch/AuroraArch.blend`.
- `AuroraArchAuthoring.Apply()`, or **Tower of Babel → Buildings → Apply aurora arch**, does the following:
  - imports the models with axis conversion baked;
  - builds `AuroraArchChunks.asset` and `Aero_AuroraArch.mat`;
  - rewires `Arch.prefab` (stages, motion library, material, and a non-convex collider from the completed mesh);
  - writes `AuroraArchInspection.json`.
- `AuroraArchAuthoring.RepairTowerRings()`, or **Tower of Babel → Buildings → Repair pillar and arch rings**, rewrites only the saved tower's cached pillars and arches from `RingLayout`, then rebuilds pillar supports with `TowerSupportAuthoring.Apply()`. Results are in `AuroraArchRingRepair.json`:
  - 21 duplicate pillar/arch pairs were removed (8,220 → 8,199 of each);
  - the remaining pillars moved by at most 1.6 mm;
  - floors, glass floors and stairs are unchanged;
  - supports are back to 1,435 ledges, with 1,505 pillars resting on arches and 164 bushes.

  Do not use **Configure Tower Data** for this: a full regeneration also rebuilds the floor and glass-floor tiles, and those differ from the authored ones saved in the scene.
- `AuroraArchPreview.Capture(stages, motionTime, suffix, frames, frameStep, night)` renders review images into `_Screenshots-and-previews-to-delete/Buildings/AuroraArch/`.

The previous arch (`Arch.fbx`, `ArchStages.fbx`, `ArchChunks.*`, `Aero_WhiteMarble_Arch.mat`) is left in the project unused. [Arch construction](ARCH_CONSTRUCTION.md) describes the construction method that is still in use.

## Tests

`AuroraArchEditModeTests` (17 tests) cover:

- material and prefab wiring;
- the rounded rim profile at the crown;
- the span from axis to axis, and the cap staying inside the joint;
- the opening: clear, centred, and a half-ellipse rising from the pillar tops without straight legs;
- aurora regions and the chunk library;
- ring closure with scaled arches on all 45 levels;
- the saved tower: one pillar and arch per bay on every level, with every arch ending on a pillar axis;
- rendered checks: the aurora moves, glows above the opening only in cool colours, and is continuous through a pillar joint;
- the curtain shows green plus blue or violet, and its lower edge fades in softly and rises and falls along the ring;
- the ray tips end at varied heights below the curtain, so no base line forms;
- ray lengths vary widely (short stubs to long streaks);
- the wall is plain white some of the time and lit some of the time as the patches drift past. The rendered checks sample several moments, because one spot can sit in a blank stretch.

Results on 2026-10-31:

- **EditMode:** 234 of 246 passed. The 12 failures are unrelated and were already failing before this work (sun flare, anisotropy, fonts, the Floor_Tile collider, the glass island, river projection light).
- **PlayMode** (automated runner only, after implementation, rerun after the rounded rim): 58 of 62 passed. The 4 failures are the ones that were already failing before this work (click feedback, menu font, hover light, glass-floor cookie). In an earlier run three keyboard tests and one mouse-hover test also failed; on this rerun all four passed, so those were environment flakes, not caused by the arch.

## Open items

- How bright the aurora should be, and its day/night balance, still need judging in the live game lighting. The captures use a simple preview scene.
- Green and pink are an exception to the building kit's white-and-blue palette, made at the user's request for the aurora only.
- Live multiplayer construction timing and performance on target devices are unverified, as for the pillar.
