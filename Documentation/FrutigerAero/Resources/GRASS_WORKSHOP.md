# Grass and Living Grass

Requested on 2026-10-31 for the detail vines. Grass is a base resource gathered from meadow tufts. The grass planter grows it into **Living Grass**, which builds the vines of each floor's detail phase.

| Raw material | Processed material | Processor | Consumed by |
| --- | --- | --- | --- |
| Grass | Living Grass | Grass planter | Detail vines ([vines](../Foliage/VINES.md)) |

## Gameplay values

- **Types:** Grass is 10 and Living Grass is 11, appended so saved and networked values never change. The readable name is "Living Grass".
- **Gathering:** 2 grass per gather, 2.6 s before upgrades, 6 s respawn. The node ID is 3004.
- **Processing:** 2 grass → 1 living grass over 3.5 s at the planter (`tower-grass-planter-01`).
- **Placement:** the Tower scene continues the workshop row past the sky spinner at z = −101.5: the grass tuft at x 25.0 and the planter at x 28.2. Heights come from a ground raycast.
- **Cheats (F3 → Inventory):** "Gain grass" and "Gain living grass".

Exchanges, capacity, XP and upgrades use the existing server-authoritative resource flow.

## Grass tuft (`Grass.prefab`)

- **Tuft:** a mound of 230 curved, tapering, double-sided blades about 1.5 m across, taller in the middle. A few blades are yellow-green, and small white flowers sit among them.
- **Bed:** a lumpy moss bed with a few pebbles. It stays through cooldown.
- **Wind:** blades sway in the Buoyant Bush shader, weighted by vertex alpha.
- **Gathering:** it uses the sand-pile feedback. The tuft is pulled down and narrows while clippings and glowing pollen motes fly. On completion it hides, leaving short stubble on the bed until it regrows.
- **Sounds:** a soft blade swish per pull (`GrassStroke`) and a leafy rustle on completion (`GrassBurst`).

## Grass planter (`GrassPlanter.prefab`)

It follows the processor family layout: three status lights, a front tray (`BlockSocket`), and process kind `Grow`.
- **Body:** a pearl base with an azure band and a soil bed under a green-tinted glass dome. A mister with three arms sits on top.
- **Process:**
  - Cut grass arcs from the player onto the dome and sinks through it into the soil.
  - The mister spins and sprays.
  - New grass sprouts rise and fill the dome as green sparkles drift.
- **Completion:** a Living Grass block appears on the tray with a leaf burst and glints, only after the server confirms the exchange.

## Living Grass block (`LivingGrassBlock.prefab`)

A moss cube in a pale green glass shell, with sprouts and glowing seeds on top. It is used on the tray and for the 128 px icon.

## Authoring

Every model is generated in Unity. Nothing comes from Blender because none is installed on this machine.

From the saved, clean Tower scene, run **Tower of Babel → Resources → Apply grass and living grass** (`GrassWorkshopAuthoring.Apply()`). It:
- builds the blade and bed meshes (`Models/Resources/Grass/GrassMeshes.asset`);
- creates the materials (`Workshop_GrassBlade`, `Soil`, `GrassClip`, `LeafGlow`, `LeafGlass`), sounds, definitions, prefabs and icons;
- places or regrounds the scene instances.

`GrassWorkshopPreview.Capture()` writes a review still to `_Screenshots-and-previews-to-delete/Grass/`.

## Verification

`GrassWorkshopEditModeTests` (6 checks) covers:
- type values and names;
- definitions, icons and gather tuning;
- the grounded tuft with lasting bed and stubble;
- the planter recipe, animation and effects;
- that the vines cost Living Grass;
- store exchange and cheats.
