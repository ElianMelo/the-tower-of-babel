# Sand, glass and gathering feedback

> **Superseded kiln (2026-10-31):** the glass kiln below has been replaced by the dome kiln of the [processor family](PROCESSOR_FAMILY.md). `SandGlassWorkshopAuthoring` now only builds the sand node. The kiln notes below are historical.

The later [essence workshop](ESSENCE_WORKSHOP.md) adds river, water and sky chains. It also makes glass the stair step material.

The Tower scene has a sand pile beside the existing stone station and a new round glass kiln. Gather with the usual **E** action: one sand interaction grants **2 sand**, takes **2.6 seconds** before upgrades, and respawns after **6 seconds**. The kiln converts **2 sand into 1 glass** over **3.5 seconds**. Inventory rows, interaction costs/rewards, profession upgrades, XP and bag capacity use the existing server-authoritative resource flow. F3 Inventory includes **Gain sand** and **Gain glass**; instant processing accepts the kiln recipe too.

## Models and placement

- Blender source: `ArtSource/Resources/Sand_And_Glass.blend`, scene `Aero_SandGlassWorkshop`.
- Rebuild script: `Tools/BuildingAero/build_sand_glass.py`, executed through Blender MCP. It creates the sand mound, kiln, low-poly rock chip and beveled spawner brick, then exports FBX models to `Assets/_Project/Models/Resources/`.
- Unity authoring: `SandGlassWorkshopAuthoring.Apply()` imports/remaps project-owned materials, makes shared sounds and icons, updates the original stone prefab, and creates `Sand.prefab` and `GlassFurnace.prefab`. Re-running it preserves existing scene placements.
- Sand node ID is 2001; scene kiln ID is `tower-glass-kiln-01`. Assign different IDs to additional copies.
- New scene placements are approximately (-2.4, 0.03, -101.5) and (1.2, 0.03, -101.5), beside the original resource area. Existing stone/processor positions remain unchanged.

The sand mound has an irregular rippled outline and a small shared grain texture. The kiln has pearl ceramic bands, azure metal, a tall ribbed chimney, an amber window, a side flywheel and bellows, and a tray for a reflective cyan glass slab. Moving assemblies are joined by part to keep renderer counts low. FBX transforms are baked so animation axes match Unity's Y-up convention.

## Gathering feel

`ResourceHarvestFeedback` adds an initial stroke and seven progress accents. Stone strokes combine a short squash/recoil in its render-only presentation, pitched impact sounds, three faceted mesh chips and a small dust puff. Confirmed destruction sends 24 chips and a broader dust burst outward. Chips spin, fall under gravity and bounce against one local ground plane, without adding Rigidbody objects or moving the resource collider.

Four small bricks appear at the original rock base while the node is cooling down. They have no colliders and disappear on respawn. Duplicate cooldown notifications do not replay the destruction burst. Respawn adds a short rise into full size, a small dust puff and a quiet chime. Cheat reset clears markers, particles and presentation immediately.

Sand stays anchored to its base while its render-only pile narrows to 60% width and shrinks to 16% height across the action. Each stroke emits fine three-dimensional grains, tan dust and a soft granular sound. Cancellation/rejection restores the intact pile and grants nothing. Authoritative completion hides the pile, emits the final dust/grain burst and starts normal respawn. Original transforms and interaction colliders remain fixed throughout.

## Kiln cycle

`GlassFurnaceAnimation` compresses and expands the bellows, accelerates the flywheel, lifts the hot lid slightly, pulses amber progress lights and fades in a low motor sound. Sand drops onto the lid, sinks and shrinks during heating; a molten sample appears on the tray after 18% progress. The chimney produces a few rising embers and soft vapour wisps during work.

Cancellation stops the feed and emission without showing a glass reward. Only the server-confirmed exchange reveals the glass slab: it grows onto the tray, lifts/turns gently, slides outward and collects away over 1.25 seconds. Small glass glints and a layered chime accent the confirmation. Duplicate completion notifications do not restart the visual reward. Work animation is local presentation; resource counts and exchange validation remain authoritative.

## Cost and verification

Each particle system is capped at **48 mesh particles or 64 billboards**. Effects reuse the authored systems, shared meshes, materials and short mono audio clips. Idle nodes emit nothing. There are no per-hit GameObject instantiations, particle lights, physics bodies or world-mesh collision scans. A whole kiln uses at most 160 particles across its three systems; gathering uses at most 112 per active node.

`SandGlassTerrainEditModeTests` checks definitions/icons, the recipe, fixed colliders, cancellation/presentation, particle limits and terrain budgets. Additional `ResourceProcessorPlayModeTests` use a local host to exercise sand gathering/cancellation/respawn, atomic sand-to-glass conversion, full/missing inventory, duplicate feedback and rock cooldown bricks. `CheatMenuPlayModeTests` now covers both new resource buttons. See `SandGlassValidation.json` for the final results.

The focused final run passed **63 Edit Mode checks and 16 Play Mode checks**, with no failed or skipped checks. This is not a full-suite claim or an FPS benchmark.

Review: [scene placement](Previews/Sand-Glass-World.png), [idle lineup](Previews/Sand-Glass-Idle.png), [working](Previews/Sand-Glass-Working.png), [completion and rock marker](Previews/Sand-Glass-Complete.png), [sampled motion](Previews/Sand-Glass-Working.gif). These are disposable Edit Mode samples; the saved Tower scene and camera are restored afterward.
