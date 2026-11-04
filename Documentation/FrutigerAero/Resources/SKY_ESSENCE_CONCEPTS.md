# Sky essence resource concepts

Requested on 2026-11-04, together with the Wind Condenser variants ([sky processor concepts](SKY_PROCESSOR_CONCEPTS.md)). These are 10 cloud-and-wind designs to replace the current `SkyCloud` node. They are static concepts: nothing is animated or imported into Unity yet.

Every design follows the essence-node rules in the [essence workshop](ESSENCE_WORKSHOP.md):
- it is a grounded, natural resource about 2 m across;
- it has no machine parts;
- it has two groups under its root:
  - **Essence** is the part that is gathered and hides during cooldown. It is built from the real `SkyCloud` tufts and mound.
  - **Base** is what stays behind.

| # | Design | Essence (gathered) | Base (stays) |
| --- | --- | --- | --- |
| 01 | Cumulus Nest | A fat cumulus with drifting tufts and glow motes | A moss bed, a ring of grass and white pebbles |
| 02 | Dust Devil | A spiral of cloud rising and widening, with three wind ribbons and a cloud cap | Grass swirled around the centre, and flat stones |
| 03 | Cloud Dandelions | Three dandelion clocks with cloud seed heads, and seeds blowing away on a breeze | A leaf rosette and grass |
| 04 | Cotton Cloud Bush | Cloud bolls on the branch tips | A pale twig shrub with round leaves, on moss |
| 05 | Sky Vent | A column of cloud breathed up from the vent, with a swirl ribbon | A sky-stone vent mound with a glowing mouth, sky crystal shards and pebbles |
| 06 | Mirror Pond | A cloud resting on the water, with a low ring of mist | A sky-mirror pond, pebble edge, reeds, cattails and lily pads |
| 07 | Crystal Geode | Cloud wisps and cloud-filled bubbles caught among the crystals | Sky-stone rocks and a cluster of sky crystals |
| 08 | Breeze Sheaf | Two streams of cloud blowing through the grass, with gust ribbons | Tall grass bent by the wind, short grass and stones |
| 09 | Cloud Bubbles | Seven iridescent bubbles, each holding a cloud | A moss mound with grass, pebbles and leaves |
| 10 | Wind Arch | A cloud stream flowing through the arch, with a cloud pooled beneath | A wind-carved sky-stone arch, and grass |

Most designs have 9–26 k faces. This is concept density; a chosen design would be simplified for the game, like the current node (6.3 k faces).

## Authoring

- **Blender:** `Tools/BuildingAero/build_sky_essence_concepts.py` builds the scene `Aero_SkyEssenceConcepts` with the shared `sky_concept_kit.py`. It runs in the open Blender through the MCP socket.
- **Render and save:** `render_sky_processor_concepts.py`, run with `SCENE_NAME = 'Aero_SkyEssenceConcepts'`, `TOP_NAME = 'SkyEssenceConcepts'` and `PREFIX = 'SkyEssence'`. It saves `ArtSource/Resources/SkyEssenceConcepts/Round01.blend`.
- **Sheet:** `assemble_sky_processor_concepts.py essence01` writes `_Screenshots-and-previews-to-delete/SkyEssenceConcepts/Round01/SkyEssence_Concepts_Round01.png`.

## Outcome

On 2026-11-04 the user chose **03 Cloud Dandelions**. It is now the sky essence node; see [sky workshop](SKY_WORKSHOP.md).
