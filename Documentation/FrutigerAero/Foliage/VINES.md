# Detail vines

Requested on 2026-10-31. Vines are the detail phase of each floor: once a floor's structure is complete, players build vines that climb a pillar from the deck and spill onto part of its arches, inside or outside the tower. They replace the earlier placeholder detail bushes. References: the **Climbing vines** section of [the reference index](../../../Frutiger%20Reference/INDEX.md).

## Shapes

All vines are generated procedurally by `VineGrowthBuilder` over the real pillar and arch meshes. There are three shapes, each a separate tower asset type so the near pool and the distant renderer batch them like other buildings:

| Type | Prefab | Character | Triangles at stage 10 |
| --- | --- | --- | --- |
| `Vine` | `Detail_Vine_Climbing` | Two stems climb the pillar and reach across both neighbouring arches. A few strands hang into the openings. | 3.7 k |
| `VineDraped` | `Detail_Vine_Draped` | Stems run along the top of one arch, with long strands curtaining down into its opening. | 3.6 k |
| `VineDense` | `Detail_Vine_Dense` | Three stems, some stopping low, cover the pillar thickly and fill the spandrel beside it. | 5.6 k |

**How a vine grows:**
- Each stem starts at the deck and walks up the pillar's front face, about 2.5 cm off the surface. It steers around missing support, so it never floats.
- Above about 3.5–3.8 m it leans sideways, works up the spandrel around the arch opening, and spreads along the arch.
- Side branches leave the stems; high on the arch some trail down the face.
- Loose strands fall from the edge of the arch opening. They are the only parts that hang free.
- Ivy leaves have three lobes and hang tip-down, tilted from the wall. Colours run from deep ivy green at the base to lime and yellow-green new growth, with a few red leaves like Virginia creeper.

## Construction stages

Every stem point and leaf remembers its distance from the root along the stems. Stage *k* shows the vine grown to `VineGrowthBuilder.GrowthAt(k)` of its full length:
- Stage 0 is a small sprout at the foot of the pillar (7% of the length).
- Stage 10 is the full vine.
- Leaves at the growth front start small and open up as the vine grows past them.

All 11 stage meshes of each shape are saved in `Assets/_Project/Models/Foliage/DetailVine_<Shape>.asset`.

## Placement

`TowerGenerator.BuildDetailVines` places one vine on a random pillar in every group of six (`detailPillarInterval`), using a fixed seed (`detailSeed`). Each vine gets:
- a random shape;
- the inside or outside face of the tower;
- a 50% chance of being mirrored, so it spreads onto the arch on either side.

Vines are rooted at the pillar position and are not offset further, because they cling to the pillar itself.

The stair ramp runs along the inside of every pillar, so an interior vine would usually cross it. A face is rejected if a step crosses the vine anywhere above its lowest 0.8 m (`StairRootAllowance`). Where a step only meets the root, the vine looks like it grows from the step edge. If both faces are blocked, the pillar is skipped.

**Current tower:**
- 1,303 vines: 408 climbing, 478 draped and 417 dense.
- 46 on the ground floor, tapering to 14 at the top.
- 73 face inside and 647 are mirrored.

## Rendering and interaction

- **Shader:** `TowerOfBabel/Foliage/Clinging Vine` (`AeroVine.shader`) is the Buoyant Bush leaf lighting, made double-sided:
  - Normals are flipped toward the camera, so mirrored instances light correctly.
  - Wind only flutters leaf rims and hanging strands, weighted by vertex alpha. Stems clinging to the stone never move.
- **Material:** `AeroVineLeaf.mat`, with GPU instancing.
- **Collider:** a box covering only the vine on the pillar face, so the arch opening stays walkable.
- **Cost:** 1 Living Grass per stage, 2.5 s per stage before upgrades.

## Authoring

With the Tower scene open, run **Tower of Babel → Construction → Grow detail vines and place them on pillars** (`DetailVineAuthoring.Apply()`). It:
1. samples the pillar and arch meshes in a preview physics scene;
2. grows the three shapes and saves their stages, material and prefabs;
3. assigns them to the generator and the chunk manager;
4. replaces only the cached detail data, leaving all other assets and their stages untouched.

`ConfigureTower` also places vines when it regenerates the whole tower.

`DetailVineAuthoring.Capture()` writes review stills of the shapes, a close-up and the growth stages to `_Screenshots-and-previews-to-delete/Vines/`.

## Verification

- `VineGrowthEditModeTests` (5 checks) uses an analytic pillar-and-arch facade. It checks determinism, growth from sprout to arch, that every vertex stays in front of the facade, hanging strands, and still stems with fluttering leaves.
- `DetailPhaseEditModeTests` covers phase gating and generator placement:
  - faces and mirroring;
  - stair rejection with the root allowance;
  - determinism.
- `GrassWorkshopEditModeTests.Vines_AreBuiltFromLivingGrassAndGrowEachStage` checks the prefabs: their cost, monotonic stage growth and triangle budget.
