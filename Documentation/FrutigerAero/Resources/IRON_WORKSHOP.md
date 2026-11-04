# Iron Stone and Iron Ingot

Requested on 2026-11-04 to make the tower's lava lamps buildable. Iron Stone is a base resource gathered from an ore boulder. The iron smelter melts it into **Iron Ingots**, which build the [lava lamps](../Lighting/LAVA_LAMPS.md#building-the-lamps) of each floor's detail phase. The same request reworked the stone node's model.

| Raw material | Processed material | Processor | Consumed by |
| --- | --- | --- | --- |
| Iron Stone | Iron Ingot | Iron smelter | Lava lamps, 1 ingot per stage |

## Gameplay values

- **Types:** Iron Stone is 12 and Iron Ingot is 13, appended so saved and networked values never change. Readable names: "Iron Stone" and "Iron Ingot".
- **Gathering:** 1 iron stone per gather, 2.2 s before upgrades, 5 s respawn. Node ID 4001.
- **Processing:** 2 iron stone → 1 iron ingot over 4 s at the smelter (`tower-iron-smelter-01`).
- **Placement:** the Tower scene continues the workshop row past the grass planter at z = −101.5: the iron stone at x 31.8 (in the **Resources** folder) and the smelter at x 35.2 (in **Processors**). Heights come from a ground raycast.
- **Cheats (F3 → Inventory):** "Gain iron stone" and "Gain iron ingot" (`CheatCommand.AddIronStone`, `AddIronIngot`, appended).

Exchanges, capacity, XP and upgrades use the existing server-authoritative resource flow.

## Iron stone (`IronStone.prefab`)

- **Model** (`IronStoneCluster.fbx`): three rust-red split boulders with flat facets, studded with ten dark metallic ore nodules, with rust streaks running down in bands. A few ore pebbles lie around it.
- **Materials:** `Resource_IronRock` (the stone texture tinted rust red), `Workshop_Rust`, `Workshop_IronOre` (metallic 0.85).
- **Gathering:** it starts as a copy of the stone node, so it weaves and throws chips like the stone. The chips are rust-coloured, the dust is rusty brown, and the bricks left during cooldown are ore.

## Iron smelter (`IronSmelter.prefab`)

The sixth member of the [processor family](PROCESSOR_FAMILY.md), with process kind `Smelt` (appended to `ProcessKind`). It keeps the family layout: a top at about 0.56 m, three status lights and a front output point (`BlockSocket`).

- **Base:** a cast-iron plinth with an azure deck rim and pearl bolts, a runner glowing with molten iron, and a casting mould on a steel table. Three molten-orange status lamps sit on the deck.
- **Body:** a pearl furnace with a domed top and azure bands, a hopper on the dome and a chimney behind. On the front, a steel sight-glass housing with clear glass over a dark crucible plate. Three glowing heat coils ring the furnace. A side cooling fan sits in an azure ring, a temperature dial is on the left, and a stamp press on a small gantry hangs over the mould.
- **Process:**
  - An ore chunk is lobbed from in front of the machine into the hopper and sinks into the furnace (25–60% of the cycle).
  - The heat coils glow with progress, the cooling fan spins up and the temperature dial climbs.
  - Molten iron rises in the sight glass (25–68%), then drains to a quarter as it runs down into the mould (70–86%).
  - The stamp strikes the cast once (86–97%) with a ringing clang (`IronClang`) and a burst of sparks.
  - Sparks fly at the sight glass and the chimney smokes while it works.
- **Completion:** an iron ingot appears in the mould and slides forward, only after the server confirms the exchange.
- **Size:** 5.6 k faces; under the water press, which stays the tallest machine.

## Iron ingot (`IronIngot.fbx`)

A bevelled trapezoid bar in bright steel (`Workshop_Ingot`, metallic 0.95) with a small azure diamond stamp on top. It is shown on the smelter's tray and rendered for the 128 px icon.

## Reworked stone

The stone node (`Stone.prefab`) keeps its GUID, definition, feedback and scene placement and gets a new model (`StoneCluster.fbx`): three split boulders with flat chiselled facets, moss on their upward faces, four pebbles and three small quartz shards. Materials: `Resource_Stone` (the existing stone texture), `Workshop_Moss`, `Workshop_Pearl`. The stone's hand-made UI icon is unchanged.

## Authoring

- **Blender:** `Tools/BuildingAero/build_iron_workshop.py` builds all four models and saves `ArtSource/Resources/Iron_Workshop.blend`. It runs through the Blender MCP socket or headless: `blender -b --factory-startup --python Tools/BuildingAero/build_iron_workshop.py`. The rock FBXs hold only their mesh, with Y-up vertices, because the nodes reuse the mesh alone; the ingot and smelter keep their hierarchy so the moving parts' pivots stay correct.
- **Unity:** from the saved, clean Tower scene, run **Tower of Babel → Resources → Apply iron stone, smelter and ingot** (`IronWorkshopAuthoring.Apply()`). It creates the materials and sound, imports the models, writes the definitions, puts the reworked model on the stone node, builds the iron stone node and the smelter (through `ProcessorFamilyAuthoring`, which also builds it with the rest of the family), places or regrounds the scene instances, and renders the icons.
- **Then** run **Tower of Babel → Environment → Apply tower lava lamps** to build the lamp stages and add the buildable lamps to the tower.

`IronWorkshopPreview.Capture(progress)` writes a review still of the stone, the iron stone and the smelter mid-cycle to `_Screenshots-and-previews-to-delete/IronWorkshop/`.

## Verification

- EditMode `IronWorkshopEditModeTests` (8): type values and names; definitions, icons and gather tuning; the reworked stone on the ground; the iron stone node (unique ID, model, materials, rock feedback with ore bricks and rust chips); the smelter recipe, motion mix and heat coils; its work cycle (melt, drain into the mould, one stamp strike); store exchange and cheats; one node and one smelter in the Tower scene, grouped and on the ground.
- EditMode `ProcessorFamilyEditModeTests` now covers six machines, including the smelter's base materials, output spot, silhouette and full work cycle.
- PlayMode `ResourceProcessorPlayModeTests.HostIron_GatherThenSmeltIntoAnIngotExactlyOnce` and the iron cheats in `CheatMenuPlayModeTests` (automated runner only).
