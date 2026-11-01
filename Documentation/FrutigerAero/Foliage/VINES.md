# Detail vines

Requested on 2026-10-31, revised twice on 2026-11-01 (outside/inside, colour, patches; then wider spread and pillar-only and arch-only shapes). Vines are the detail phase of each floor. Once a floor's structure is complete, players build vines that climb a pillar from the deck and spill onto part of its arches. They replace the earlier placeholder detail bushes. References: the **Climbing vines** section of [the reference index](../../../Frutiger%20Reference/INDEX.md).

## Shapes

All vines are generated procedurally by `VineGrowthBuilder` over the real pillar and arch meshes. There are twelve shapes (`TowerAssetType.Vine0`–`Vine11`, prefabs `Detail_Vine_0`–`Detail_Vine_11`). Each is its own tower asset type, so the near pool and the distant renderer batch them like other buildings.

The shapes cycle through six archetypes, and every other setting is randomized per shape (`DetailVineAuthoring.RandomStyle`):

| Archetype | Shapes | Character |
| --- | --- | --- |
| Climbing | 0, 6 | One to three stems climb the pillar and reach far across the arches. |
| Draped | 1, 7 | Stems run along the arches, with long strands curtaining into the openings. |
| Dense | 2, 8 | Three stems, some stopping low, cover the pillar thickly and fan out over the spandrels. |
| Sparse | 3, 9 | A lighter vine with a shorter reach. |
| Pillar only | 4, 10 | Two to four stems cover the pillar and stop under the capital, never reaching the arch. |
| Arch only | 5, 11 | Rooted on the spandrel above the capital, it spreads and drapes along the arch and leaves the pillar bare. |

**Randomized per shape:**
- stem count;
- where each stem leaves the pillar (it varies from stem to stem);
- how far it spreads and how much it meanders;
- which side it favours;
- how many strands hang into the arch openings;
- leaf density and size, and the share of red leaves.

**Wide spread:**
- The sampled facade includes the neighbouring pillars and two arches on each side. Stems can therefore run past the next pillar, and the widest shapes reach 5–7 m across.
- On the arch, branches are more frequent and almost twice as long, so the vine fans out.

**Triangle budget:** each shape stays near 6,000 triangles when fully grown (`DetailVineAuthoring.TriangleBudget`). A shape that grows over the budget is regrown with thinner leaves and branches. The current shapes range from 1.9 k to 5.6 k, about 3.9 k on average.

**How a vine grows:**
- Each stem starts at the deck (or, for arch-only vines, on the spandrel) and walks up the facade about 2.5 cm off the surface. It steers around missing support, so it never floats.
- Above about 3.3–4 m it leans sideways, works up the spandrel around the arch opening, and spreads along the arch.
- Side branches leave the stems; high on the arch some trail down the face.
- Loose strands fall from the edge of the arch opening. They are the only parts that hang free.
- Ivy leaves have three lobes and hang tip-down. Colours run from ivy green at the base to lime and yellow-green new growth, with a few red leaves.

## Construction stages

Every stem point and leaf remembers its distance from the root along the stems. Stage *k* shows the vine grown to `VineGrowthBuilder.GrowthAt(k)` of its full length:
- Stage 0 is a small sprout: at the foot of the pillar, or above the capital for arch-only vines (7% of the length).
- Stage 10 is the full vine.
- Leaves at the growth front start small and open up as the vine grows past them.

## Placement: separate outside and inside vines

`TowerGenerator.BuildDetailVines` considers the **outside and inside face of every pillar separately**, so each face is its own building block. A pillar can have none, one or both.

- **Coverage:** about 22% of all pillar faces (`detailVineCoverage`).
- **Patches:** the density varies smoothly around each ring, so vines gather into patches with bare stretches between them (`detailVineClustering`). The two faces use different patch patterns.
- **Per vine:** a random shape, and a 50% chance of being mirrored so it spreads onto either arch.
- **Determinism:** placement uses a fixed seed (`detailSeed`).
- **Stairs:**
  - Outside, a face is skipped if a step crosses the vine above its lowest 0.8 m.
  - Inside, vines run alongside the glass stair ramp, which follows the inside of every pillar.

**Current tower:**
- 3,597 vines: 1,842 inside, 1,755 outside, and 1,692 mirrored.
- 127 on the ground floor, tapering to about 40 at the top.

## Rendering and interaction

- **Shader:** `TowerOfBabel/Foliage/Clinging Vine` (`AeroVine.shader`) is double-sided:
  - Normals are flipped toward the camera, so mirrored instances light correctly.
  - Wind only flutters leaf rims and hanging strands. Stems clinging to the stone never move.
- **Light green at distance:** from about 12 m outward, leaves lift toward a bright lime (`_DistanceLift`) and gain a soft glow (`_DistanceGlow`). Without this, thin stems and shaded leaf sides blend into a dark mass.
- **Ambient light on distant vines:** the scene has no baked light probes, so distant instanced pages used to receive no ambient light at all, which made far vines dark. Detail batches now get the scene's ambient probe (`InstancedFarChunkRenderer.AmbientProbeProperties`), rebuilt only when the ambient changes. The shader also falls back to the scene's trilight ambient if it ever gets none.
- **Draw distance:** distant vine pages stop drawing beyond 140 m (`detailDrawDistance` on the far renderer), so thousands of vines stay affordable.
- **Collider:** a thin box over the vine on the pillar face, so the arch opening and the stair ramp stay walkable. Arch-only vines sit above head height, so their box covers the vine on the arch face instead.
- **Cost:** 1 Living Grass per stage, 2.5 s per stage before upgrades.

## Authoring

With the Tower scene open, run **Tower of Babel → Construction → Grow detail vines and place them on pillars** (`DetailVineAuthoring.Apply()`). It:
1. samples the pillar and arch meshes in a preview physics scene;
2. grows the twelve shapes and saves their stages, material and prefabs;
3. assigns them to the generator and the chunk manager;
4. replaces only the cached detail data.

`ConfigureTower` also places vines when it regenerates the whole tower.

**Review stills**, written to `_Screenshots-and-previews-to-delete/Vines/`:
- `DetailVineAuthoring.Capture()`: all shapes, a close-up and the growth stages.
- `DetailVineAuthoring.CaptureTower(floor, angle)`: rebuilds a real section of the cached tower. It renders that section from outside and inside, and compares near vines with the distant renderer's instanced path.

## Verification

- `VineGrowthEditModeTests` (7 checks): determinism, growth from sprout to arch, vertices kept in front of the facade, hanging strands, pillar-only and arch-only growth, and still stems with fluttering leaves.
- `DetailPhaseEditModeTests`:
  - phase gating and the eight-shape prefab set;
  - separate outside and inside vines;
  - patches with bare stretches;
  - outside-only stair rejection with the root allowance;
  - determinism.
- `GrassWorkshopEditModeTests.Vines_AreBuiltFromLivingGrassAndGrowEachStage`: cost, monotonic growth, every shape distinct, the triangle budget, and the presence of pillar-only, arch-only and wide-spreading shapes.
