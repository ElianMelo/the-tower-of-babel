# Essences, blocks and building materials

Three new gather → process chains join stone and sand/glass. Every tower building now consumes its own processed material. The processors for all five chains are documented in [processor family](PROCESSOR_FAMILY.md).

| Raw material | Processed material | Processor | Building that consumes it |
| --- | --- | --- | --- |
| Sand | Glass | Glass kiln | Stair step (`Step_Tile`) |
| River Essence | River Block | River mill | Water floor (`GlassWaterFloor`) |
| Water Essence | Water Block | Water press | Pillar |
| Stone | Processed Stone | Stone processor | Floor tile (`Floor_Tile`) |
| Sky Essence | Sky Block | Sky spinner | Arch |

Each building still costs 1 material per construction stage, before upgrades. The interaction card and the "Need …" prompt show readable names ("River Block", "Processed Stone") and the material's icon.

## Gameplay values

- **Essence nodes:** 2 essence per gather, 2.8 s before upgrades, 6 s respawn. Node IDs 3001 (river), 3002 (water), 3003 (sky).
- **Processing:** 2 essence → 1 block over 3.5 s at the matching processor.
- **Resource type values:** River Essence 4, River Block 5, Water Essence 6, Water Block 7, Sky Essence 8, Sky Block 9. These are network and save identities; append new types, never renumber.
- **Placement:** the Tower scene continues the workshop row east of the glass kiln at z = −101.5. The pairs are river at x 5.2 / 8.4, water at 11.8 / 15.0 and sky at 18.4 / 21.6. Heights come from a ground raycast.
- **Cheats (F3 → Inventory):** "Gain river essence", "Gain river block", and the same for water and sky.

All exchanges, capacity checks, XP and upgrades use the existing server-authoritative resource flow. The animations below are local presentation only.

## Essence nodes: natural, like stone and sand

On 2026-10-31 the essences were reworked to follow the stone cluster and sand mound. They are now grounded natural resources about 2 m across, with no pedestals or machine parts. Each node has two parts:
- an **essence** that is gathered and hides during cooldown;
- a **base** that stays, the way the stone leaves bricks behind.

| Essence | Gathered part | Base that stays | Icon |
| --- | --- | --- | --- |
| River Essence (`RiverPool.fbx`) | A teal spring pool with a glowing spiral current and three drifting wisps | A ring of smooth river pebbles around a pebble bed | `RiverEssence-Icon.png` |
| Water Essence (`WaterBubbles.fbx`) | A wobbling clump of clear water bubbles with foam inside, and four loose droplets | A moss patch with small pebbles | `WaterEssence-Icon.png` |
| Sky Essence (`SkyCloud.fbx`) | A fallen cloud mound with four drifting cloud tufts | Glowing sky crystal shards and white pebbles | `SkyEssence-Icon.png` |

The processed blocks keep their models:
- `RiverBlock.fbx`: rounded teal glass with wave ribbons.
- `WaterBlock.fbx`: blue glass with bubbles.
- `SkyBlock.fbx`: pale glass with a cloud and a star.

All icons are 128 px sprites rendered from the models.

**Authoring:**
- **Blender:** `Tools/BuildingAero/build_essence_workshop.py`. The source file is `ArtSource/Resources/Essence_Nodes_And_Blocks.blend`.
- **Unity:** **Tower of Babel → Resources → Apply essence nodes, blocks and building costs** (`EssenceWorkshopAuthoring.Apply()`), from the saved, clean Tower scene. It builds materials (new: `Workshop_RiverStone`, `Workshop_Moss`), prefabs, definitions, icons and building costs. It places missing nodes and puts existing ones on the ground.

## Gathering animation (`EssenceHarvestFeedback`)

- **Idle:**
  - The river current turns slowly and the pool surface ripples gently.
  - The bubble clump wobbles like jelly.
  - The cloud breathes and sways.
  - Loose pieces bob, and the glow breathes.
- **While gathering:** it works like the sand pile.
  - The essence stays anchored to the ground while it narrows to 60% width and sinks to about 18% height.
  - The river current rides down with the sinking surface.
  - Loose pieces (wisps, droplets, tufts) lift away, shrink, and are drawn inward.
  - Six pulses emit motes and splashes, with a soft sound and a brief glow flash. The water wobble and the cloud breathing get livelier.
- **Completion:** a burst of motes and a splash chime. The essence hides during cooldown, leaving the pebbles, moss or crystals, and regrows over 0.5 s on respawn.
- **Cancel or rejection:** the full-size essence returns and nothing is granted. Duplicate depletion notices don't replay the burst.

The collider never moves, and the nodes don't use the interaction weave (strength 0), the same as the sand pile.

## Cost

- **Particles:** each node has two particle systems capped at 48 mesh particles, with nothing emitted at idle.
- **Shared assets:** one 20-face mote mesh is shared by every node.
- **Polygons:** river 3.0 k, water 3.9 k and sky 6.3 k faces.

## Verification

- **EditMode:** `EssenceWorkshopEditModeTests` has 8 checks:
  - type values and readable names, definitions and icons, and building costs;
  - that the nodes are grounded and natural (no machine materials, low and about 2 m wide, body pivot on the ground, a lasting base outside the gathered part);
  - each essence's gather: the body stays anchored, sinks and narrows, loose pieces lift, cancel restores everything, and completion counts once;
  - store exchanges and cheats.
- **PlayMode:** `ResourceProcessorPlayModeTests.HostEssence_GatherThenCondenseIntoItsBlockExactlyOnce` runs a host gather → cancel → gather → respawn, then processes the essence into its block, for all three chains.

Review stills (with stone and sand for comparison) are written by `ResourceStationsPreview.Capture()` to `_Screenshots-and-previews-to-delete/ResourceStations/`.
