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

## Round02: Frutiger Aero dandelion designs

Requested on 2026-11-15, with a photo of a real dandelion clock (`Frutiger Reference/Asset/Dandelion-Clock-User.webp`). The user asked for 10 designs that keep the current Cloud Dandelions node but look more like a real dandelion and more Frutiger Aero. One will be chosen before the animations and the final model are made. These are static concepts; nothing is imported into Unity yet.

**What all ten keep from the current node** (`SkyDandelions.fbx`):
- the hierarchy: **Base** holds `Rosette` and `Grass`; **Essence** holds `Bloom`, with `Stalk_1` and `Stalk_2` as its children, and `Seed_0`–`Seed_4`;
- the stalk bases, heights and leans. Each stalk is an empty at its base holding `Stem` and `Head`, so the current SkyBloom bow, shrink and regrow still fit;
- the rosette layout: nine leaves, the crown, the bud, the yellow bloom, pebbles and the grass ring.

**What all ten change:**
- **Heads.** The cloud balls are replaced by real clocks: a receptacle, about 100 achenes on a golden spiral, thin beaks, and pappus umbrellas of 14 curled bristles. Reflexed bracts hang under each head. Heads are a little larger: radius 0.2, 0.175 and 0.165 m, against 0.17, 0.15 and 0.14 m before.
- **Leaves.** The ellipses become toothed "lion's tooth" dandelion leaves: arched and cupped, with backward-pointing lobes, a pale midrib, chevron veins and a glossy clear coat.
- **Base details.** The bud has hugging and curled bracts. The yellow bloom has four rings of strap florets. The pebbles are pearl and aqua.
- **Drifting seeds.** Each is a single parachute seed. They sit at the production spots, moved clear of the larger heads.

| # | Design | Frutiger finish |
| --- | --- | --- |
| 01 | Silk Clock | The realistic baseline: silky white pappus and glossy aqua achenes |
| 02 | Dewdrop Clock | Glass dew beads on the seed umbrellas, bristle tips, leaves and drifting seeds, with a few glints |
| 03 | Bubble Clock | Each head and each drifting seed sealed in an iridescent soap bubble; loose bubbles in the grass |
| 04 | Fiber Optic Clock | Bristles fade from white to glowing cyan tips, like a fibre-optic lamp; glowing core; fireflies |
| 05 | Iridescent Clock | Pastel thin-film tips (cyan, lilac, pink, mint) that vary seed by seed; pearl achenes |
| 06 | Cloud Halo Clock | Keeps a SkyCloud tuft as the core of each head, inside the silk seeds and a soft halo rim; each drifting seed carries a small cloud |
| 07 | Crystal Clock | Sky-crystal achenes and core, frosted blue glass fibres, glass stems with a green core, hexagonal crystal clusters in the base |
| 08 | Half Blown Clock | Like the photo: a full head, a half-blown head showing its pitted dome, and a closed head. Seeds ride a breeze ribbon. Previews the gather states |
| 09 | Jelly Clock | Y2K candy: lime-jelly stems, bondi-blue gel achenes, fewer and softer bristles with pearl bead tips, candy-gel leaves and pebbles |
| 10 | Sparkle Clock | Silk heads with four-point star glints, soft cyan, lime and white bokeh orbs, and pollen motes |

Designs have 30–77 k faces at concept density. The bristles are thin prisms; a chosen design would be reduced for the game, for example to cards with a pappus texture.

### Authoring

- **Blender:** `Tools/BuildingAero/build_sky_dandelion_designs.py` builds the scene `Aero_SkyDandelionDesigns` with `sky_concept_kit.py`. It can run through the Blender MCP socket, or headless: Blender 5.3 has a window in background mode, so the kit works unchanged.
- **Render and save:** after the build, run `render_sky_processor_concepts.py` with these settings:

  ```
  SCENE_NAME = 'Aero_SkyDandelionDesigns'
  TOP_NAME = 'SkyDandelionDesigns'
  PREFIX = 'SkyDandelion'
  SHOT_DIR = '/_Screenshots-and-previews-to-delete/SkyEssenceConcepts/Round02'
  BLEND_PATH = '/ArtSource/Resources/SkyEssenceConcepts/Round02.blend'
  RES = (1200, 1000)
  MIN_SIZE = 2.05
  SAMPLES = 96
  GLARE = .1
  EXPOSURE = -.35
  ```

  `RES`, `MIN_SIZE`, `SAMPLES`, `GLARE` and `EXPOSURE` are new optional settings. Their defaults keep the earlier rounds unchanged. The run takes about 80 s.
- **Sheet:** `assemble_sky_processor_concepts.py essence02` writes `_Screenshots-and-previews-to-delete/SkyEssenceConcepts/Round02/SkyDandelion_Designs_Round02.png`.

The 2026-11-15 run was headless, because Blender 5.3 had no BlenderMCP add-on: it was only installed for 5.2, so the socket on port 9876 was closed. The helpers the designs share with the production model live in `dandelion_kit.py`.

### Outcome

On 2026-11-15 the user chose **01 Silk Clock**. They asked for leaves and grass in different sizes, more hand-painted colours on the green parts, and a smooth flower-by-flower gather. It is now the sky essence node; see [Cloud Dandelions: Silk Clock](SKY_WORKSHOP.md#cloud-dandelions-silk-clock-skyessenceprefab).
