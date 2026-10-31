# Processor family

Requested on 2026-10-31. All five processors now share one visual family, and each one looks and moves like a different machine doing a different job. This replaces the earlier stone processor, the earlier glass kiln and the three look-alike essence condensers.

## Bases built around each material

Revised on 2026-10-31 after review: the bases no longer look the same. Each one is built from its machine's material and presents the output in its own way. All five still share a family layout:
- a top at about 0.56 m;
- three status lights;
- a front output point (`BlockSocket`).

| Machine | Base | Where the output appears | Status lights |
| --- | --- | --- | --- |
| Stone processor | An octagonal quarry block (stone texture) under a steel deck with azure corner bumpers, with loose rubble around it | Slides down a pearl chute | Three round cyan lamps on the deck |
| Glass kiln | A round firebrick hearth (two staggered brick rings) on a steel foot | A steel cooling rack with pearl bars | Three amber vents |
| River mill | A riverbed: river stones ringing a shallow stream around a pearl island | A pearl flume carrying a glowing stream | Three teal pebble-lights on the island |
| Water press | A heavy steel footing and azure hydraulic platform, with bolts and a drain grate | A glass catch basin with water; the block settles half into it | Three upright gauge bars |
| Sky spinner | A pearl disc resting on cloud puffs, with a glowing ring | A glass plate hovering with a glow ring | Three floating glow orbs |

Above the base, each machine has its own silhouette and process:

| Machine | Recipe | Process | What you see |
| --- | --- | --- | --- |
| Stone processor (`StoneProcessor`) | 1 stone → 1 processed stone, 2 s | Crush | Raw stone drops into a square hopper. A gantry hammer strikes it six times, throwing grit, and two toothed rollers turn behind a glass window with dust underneath. |
| Glass kiln (`GlassFurnace`) | 2 sand → 1 glass, 3.5 s | Melt | Sand drops onto the lid of a pearl dome furnace. The bellows breathe, the lid lifts, the amber porthole heats up and fills with molten glass, and the chimney gives off embers and vapour. |
| River mill (`RiverMill`) | 2 river essence → 1 river block, 3.5 s | Mill | River essence drops into a funnel over a glowing glass sluice. A side water wheel and two gears turn, the wheel splashes, and the glass mould on the front fills with teal. |
| Water press (`WaterPress`) | 2 water essence → 1 water block, 3.5 s | Press | A tall two-pillar press. Water fills the glass cylinder, then the piston comes down and squeezes it while the pressure gauge climbs. Bubbles rise inside, and steam puffs out on completion. |
| Sky spinner (`SkySpinner`) | 2 sky essence → 1 sky block, 3.5 s | Spin | A low drum with a glass dome. Cloud essence drops onto the hub, a three-arm rotor with cloud tufts and a top fan spin up, and cloud gathers on the spindle inside the dome while wisps swirl around. |

Recipes, durations, prefab asset IDs and scene positions are unchanged. The old `RiverCondenser`, `WaterCondenser` and `SkyCondenser` prefabs were moved to their new names, so their asset IDs carry over. The stone processor was floating at y = 1.5 and now stands on the ground.

Processor IDs:
- `tower-glass-kiln-01`, `tower-river-mill-01`, `tower-water-press-01` and `tower-sky-spinner-01`.
- The stone processor keeps its original scene-path ID.

## The input enters the machine

Each cycle starts with the input in front of the machine, near where the player stands. It tumbles along an arc into the machine's intake, then is consumed inside:

| Machine | Path in | While it is consumed |
| --- | --- | --- |
| Stone processor | A stone chunk is tossed up into the hopper | It drops between the rollers as the hammer strikes |
| Glass kiln | A heap of sand lands on the hot lid | It sinks in as it melts |
| River mill | River essence drops into the funnel | It flows along the sluice to the wheel |
| Water press | A water orb arcs into the front intake funnel, then drops into the cylinder | It melts into the rising water before the piston comes down |
| Sky spinner | A cloud drifts in from the side onto the rotor hub | It is spun out across the rotor |

Entry takes the first 25–30% of the cycle, so it lasts about a second on 3.5 s recipes. Consumption ends between 45% and 88%.

## Press water

- **Glass:** the cylinder is now clear glass (`Workshop_ClearGlass`) instead of blue-tinted, so the water reads on its own.
- **Water:** the water is a deeper blue (`Workshop_WaterGlass`).
- **Surface:** the water has its own surface, with a foam meniscus and ripple rings. The surface rides on the water level as the cylinder fills, wobbles while the machine works, and drops as the piston squeezes the water to 30%.
- **Bubbles and steam:** bubbles still rise inside, and steam puffs out on completion.

## Finished materials

Each machine shows its processed material's own model on the tray, and only after the server confirms the exchange:

- `StoneBrick.fbx` for processed stone, using the existing refined stone texture.
- `GlassSlab.fbx` for glass.
- `RiverBlock.fbx`, `WaterBlock.fbx` and `SkyBlock.fbx` for the blocks.

The presentation:
- the material grows in over 0.2 s;
- it lifts and turns as it slides forward;
- it collects away over 1.3 s, with glints and a chime.

Cancelling shows nothing, and a duplicate confirmation doesn't replay it.

## One animation component

`ProcessorMachineAnimation` replaces `ProcessorWorkAnimation`, `GlassFurnaceAnimation` and the earlier `EssenceCondenserAnimation`. Each machine is a different mix of motion channels:

| Channel | What it does | Used by |
| --- | --- | --- |
| Spin | Turns a part around an axis; can speed up through the cycle | Rollers, water wheel, gears, rotor, fan |
| Travel | Moves a part once across part of the cycle, or strikes it repeatedly (each impact plays a sound and particles) | Hammer (6 strikes), kiln lid, piston |
| Pulse | Scales a part back and forth along one axis | Bellows |
| Fill | Raises a level across part of the cycle, can drain it later, and can carry a surface on top | Melt pool, mould, water column with its surface (fills, then is squeezed to 30%), cloud mass |
| Dial | Turns a needle with progress | Pressure gauge |

The component also handles:
- **Feed:** the input travels from in front of the machine into its intake (optionally through a waypoint), then moves along a path while it is consumed.
- **Output:** the finished material is shown on the tray.
- **Lights and heat:** status lights fill in sequence; heat surfaces glow with progress.
- **Effects and sound:** work, strike and completion effects, plus a motor loop and accent sounds.

Directions are given in the station's own space (Y up, −Z toward the tray), so the imported Blender axes don't matter.

## Authoring

- **Blender:** `Tools/BuildingAero/build_processor_family.py`, sent to the running Blender MCP add-on. The source file is `ArtSource/Resources/Processor_Family.blend`. Each machine has its own base function (`quarry_base`, `hearth_base`, `riverbed_base`, `platform_base`, `cushion_base`).
- **Unity:** **Tower of Babel → Resources → Apply processor family** (`ProcessorFamilyAuthoring.Apply()`), from the saved, clean Tower scene. It builds the prefabs, wires the channels, renames or removes old scene instances, and puts every machine on the ground.
- **Older tools:** `ResourceWorkshopAuthoring` and `SandGlassWorkshopAuthoring` no longer build processors, so re-running them can't overwrite the family.
- **Shared helpers:** `ResourceStationKit` holds the model, material, sound, icon and placement helpers.

**Polygons:**
- Stone processor: 2.8 k faces.
- Kiln: 5.5 k faces.
- River mill: 5.2 k faces.
- Water press: 3.4 k faces.
- Sky spinner: 10.7 k faces, mostly the cloud cushion.

Every particle system is capped at 64 and emits nothing at idle.

## Verification

`ProcessorFamilyEditModeTests` has 9 checks:
- recipes, IDs and the family layout;
- that every base is built from its own materials (stone, brick, river stone, water glass, cloud) and presents its output in its own place;
- that every machine has a different process, a different mix of motions and a different silhouette (and that the press is the tallest);
- that the input starts outside the machine on the player's side and is inside by the end of its entry;
- that the press water surface rides the level and drops as it is pressed;
- each machine's full work cycle: its own motion (strikes, wheel turn, piston travel and gauge, rotor spin, bellows and melt), the input being consumed, and the output appearing only after confirmation;
- that the Tower scene has exactly one of each machine, all on the ground.

The PlayMode host tests run the stone, glass and all three essence chains through the networked processor flow.

Review stills are written by `ResourceStationsPreview.Capture()` to `_Screenshots-and-previews-to-delete/ResourceStations/`. These captures are disposable.
