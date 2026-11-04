# Sky essence processor concepts

Requested on 2026-11-04. The user wasn't happy with the current Sky spinner ([processor family](PROCESSOR_FAMILY.md)) and asked for 10 distinct Frutiger Aero concepts to choose from. These are static concepts: nothing is animated or imported into Unity yet.

Each concept takes the real Sky Essence (a `SkyCloud.fbx` tuft) and delivers the real Sky Block (`SkyBlock.fbx`) at a front `BlockSocket`. Each one also has three status lights. Every concept differs from the others in silhouette, shader and colour.

| # | Concept | Shape | Shader and colour |
| --- | --- | --- | --- |
| 01 | Bubble Still | A sphere on an hourglass, with a side intake | Thin-film soap bubble, chrome, pink accents |
| 02 | Cloud Lava Lamp | A tall tapered lamp with a cone cap and base | Cobalt candy metal, translucent blue liquid, glowing cloud wax |
| 03 | Wind Condenser | A turbine tower over a glass cloud orb | White pearl, frosted translucent blades, grass-green gel ring |
| 04 | Gel Capsule | A horizontal pill on four legs (iMac G3 blobject) | Translucent bondi-blue candy plastic, chrome |
| 05 | Sky Balloon | A balloon envelope tethered over a basket | Striped glossy gores (sky blue and white), pearl wicker |
| 06 | Gyro Orb | Three crossed rings around a core | Gradient gel rings, deep blue gel pedestal, glowing cloud core |
| 07 | Terrarium Dome | A wide, low dome over an island | Fresnel glass dome, grass, white bark, glowing pond |
| 08 | Bloom Flower | Leaf petals around a bulb, in a pot | Translucent veined leaves (lime to deep green), aqua inner petals |
| 09 | Aurora Arch | A half-ring arch over a faceted prism | Emissive aurora gradient (green, teal, blue, violet, pink), thin-film crystal |
| 10 | Jelly Levitator | A hovering bell with ribbon tendrils | Thin-film fresnel jelly (lilac and pink), chrome pad with cyan rings |

## Round 02: Wind Condenser variants

On 2026-11-04 the user picked **03 Wind Condenser**. They asked for a better green base, more cloud effects and five variants. Every variant keeps the turbine over the glass cloud orb, the cloud tuft input, the front Sky Block and three status lights.

| # | Variant | Green base | Cloud and wind effects |
| --- | --- | --- | --- |
| 03A | Bliss Hill | A glossy rolling hill with grass blades and daisies | A tilted ring of cloud orbiting the tower, two wind ribbons spiralling up it, and cloud streaming off the blade tips |
| 03B | Leaf Rotor Pond | A meadow ringing a mirror pond, with reeds, cattails and lily pads | Four translucent leaf blades trailing cloud arcs, and mist pooled round the orb |
| 03C | Helix Vortex | Three stepped grass terraces with pearl rims | A vertical helix rotor of frosted ribbons, and a cloud tornado spinning down into the orb |
| 03D | Twin Rotor Grove | A grassy knoll with round trees, bushes and daisies | A big and a small rotor, a cumulus cap on the nacelle, and a gust ribbon weaving through both rotors |
| 03E | Sky Island | A floating grass island over a cloud bed | Cloud pouring off the rim like waterfalls, and an updraft ribbon |

Grass is real blade geometry, between 600 and 900 blades per base, with a UV gradient from dark at the root to lime at the tip. Each variant has 27–48 k faces, which is concept density, not game density.

## Authoring

The scripts run in the open Blender through the Blender MCP add-on socket (port 9876). Each round builds its own scene and leaves every other scene alone. All rounds share `Tools/BuildingAero/sky_concept_kit.py`, which holds the materials, mesh helpers, grass, terrain and wind ribbons, and the real SkyCloud and SkyBlock meshes.

- **Round 01:** `build_sky_processor_concepts.py` (scene `Aero_SkyProcessorConcepts`), saved as `ArtSource/Resources/SkyProcessorConcepts/Round01.blend`.
- **Round 02:** `build_wind_condenser_variants.py` (scene `Aero_WindCondenserVariants`), saved as `Round02_WindCondenser.blend`.
- **Render:** `render_sky_processor_concepts.py` renders each round. A caller can set `SCENE_NAME`, `TOP_NAME`, `PREFIX`, `SHOT_DIR`, `BLEND_PATH` and `LINEUP` before running it with `exec()`. It saves the round's `.blend` without changing the file that is open in Blender.
- **Sheets:** `assemble_sky_processor_concepts.py [processor01|condenser02|essence01]` makes the labelled sheets.

The stills go to `_Screenshots-and-previews-to-delete/SkyProcessorConcepts/Round01/` and `/Round02/`, with the sheets `SkyProcessor_Concepts_Round01.png` and `WindCondenser_Variants_Round02.png`.

The matching resource concepts are in [sky essence concepts](SKY_ESSENCE_CONCEPTS.md).

## Outcome

On 2026-11-04 the user chose **03A Bliss Hill**. It is now the Wind Condenser in the game; see [sky workshop](SKY_WORKSHOP.md).
