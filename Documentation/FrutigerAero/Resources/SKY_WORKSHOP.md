# Sky workshop: Wind Condenser and Cloud Dandelions

Requested on 2026-11-04. The user chose two concepts to replace the old sky essence pair:
- **Wind Condenser** replaces the Sky spinner. It comes from variant 03A (Bliss Hill) in [sky processor concepts](SKY_PROCESSOR_CONCEPTS.md).
- **Cloud Dandelions** replace the fallen cloud (`SkyCloud`). They come from concept 03 in [sky essence concepts](SKY_ESSENCE_CONCEPTS.md).

Both models were enhanced, given idle and activation motion, effects and particles, then reviewed and improved in a second pass. A third pass, after user feedback that both animations shook too much, made all motion smooth and calmer (see [review passes](#review-passes)).

On 2026-11-15 the dandelion node was rebuilt as the **Silk Clock** (design 01 of [sky essence concepts, Round02](SKY_ESSENCE_CONCEPTS.md#round02-frutiger-aero-dandelion-designs)), with a new flower-by-flower gather. See [Cloud Dandelions: Silk Clock](#cloud-dandelions-silk-clock-skyessenceprefab).

The same day, ten propositions were written and rendered to rework the Wind Condenser on the Silk Clock principle ([sky processor concepts, Round 03](SKY_PROCESSOR_CONCEPTS.md#round-03-silk-clock-processor-propositions)). The user chose **01 Silk Turbine**, without the green base and with hand-painted machine details. See [Wind Condenser: Silk Turbine](#wind-condenser-silk-turbine-windcondenserprefab).

Gameplay values are unchanged:
- **Gathering:** 2 sky essence per gather, 2.8 s, 6 s respawn, node ID 3003.
- **Processing:** 2 sky essence → 1 sky block in 3.5 s, processor ID `tower-sky-spinner-01`.
- **Placement:** the same positions in the workshop row.

The Sky spinner prefab was moved to `WindCondenser.prefab`, so its GUID carries over. The scene instance was renamed.

## Game feel: what the research says and how it is applied

The sources cover juice and game feel in general:
- the "Juice it or lose it" talk by Jonasson and Purho;
- an academic survey of juicy feedback;
- practitioner guides on tweening, hit-stop and restraint;
- a note on reward feel in idle games.

These principles shaped the work:

| Principle | How it is applied here |
| --- | --- |
| **The world is alive at rest.** Idle stations should invite interaction and never look frozen. | The rotor turns in the breeze at 30°/s. A loose ring of seeds orbits the tower, two silk spirals carry seeds down it, the cloud in the orb drifts and loose seeds float off. The dandelions nod out of step with each other, seeds drift off downwind and sparkles rise. |
| **Wind is uneven.** Constant motion reads as mechanical. | A slow layered-noise gust occasionally strengthens spin, sway and particle rates. Each placed station has its own rhythm, so a row never moves in lockstep. |
| **Anticipation, action, follow-through.** | When work starts, the orb squashes slightly over 0.5 s. Work speeds up quickly, then eases back slowly afterwards (0.3 s rise, 1.2 s settle). After completion, the rotor winds up into a brief whirl over 0.25 s, then coasts back to idle. |
| **Escalation tracks progress.** The build-up makes you anticipate the reward. | Rotor speed (35% → 100%), particle rates, the orb glow ring, the status lights (lit in sequence), the hum pitch and the pitch of each gather pull all rise with progress. |
| **Clear cause and effect.** | The essence is visible on its way in, and it is the same essence the player picked: two puffs of Silk Clock seeds rise pappus-up from the player's side into the rotor's wake, shedding loose parachutes, then are drawn down the tower into the orb, which visibly fills with cloud. |
| **A short, clear reward peak.** | On completion, the orb pops past its rest size and settles with one soft overshoot (a slow, well-damped spring). A puff of cloud and a ring of seeds blow out, glints play and a bright rising three-note chime sounds (`SkyChime`). The Sky Block appears on the tray only after the server confirms the exchange. |
| **Proportion and restraint.** Routine actions repeat hundreds of times in an incremental game. | There is no screen shake, hit-stop or vibration. Every motion is a slow, continuous curve: no oscillation runs faster than about 0.6 Hz, and impulses are eased in. Idle motion is subtle, work is medium, and the peak is clear but short (1.1 s). Particle systems are capped at 64 and emit nothing at idle unless they are ambient. |
| **Natural, not mechanical, gathering.** | The dandelions are picked one flower at a time: each bows into the breeze while its own seeds fly off, then its bare stalk sinks into the rosette as the next flower starts. There is no per-pull kick. The leaf rosette, the yellow bloom and the bud stay through cooldown. |

## Wind Condenser: Silk Turbine (`WindCondenser.prefab`)

Chosen by the user on 2026-11-15 from [Round 03](SKY_PROCESSOR_CONCEPTS.md#round-03-silk-clock-processor-propositions) (proposition 01), with two requests:
- remove the green base;
- make the machine details more hand-painted.

The prefab, its GUID, the processor ID, the recipe and the scene position are unchanged. Until 2026-11-15 the condenser was the Bliss Hill turbine: a glossy meadow hill with grass, daisies and a pebble path, a ring of cloud and two wind ribbons, fed a cloud tuft.

**Model** (`WindCondenser.fbx`, about 16 k faces as exported; `build_sky_workshop.py`):
- **No green base:** the hill, grass, daisies, path stones and dew bubbles are gone. The turbine stands on a two-tier painted plinth (`Base`), added the same day when the user asked for a better base. It has five parts:
  - **Foot:** a pearl foot that swells into twelve petal lobes, like a flower, edged with a scalloped azure band. Each petal tip carries a glass dew bead.
  - **Sky channel:** a glass channel round the drum. Six small clouds (`ChannelClouds`) drift round it.
  - **Drum:** carries the three green status lights, each set in an azure bezel.
  - **Medallion:** the drum's top is a hand-painted wind-swirl medallion (`Textures/SilkTurbineTop.png`, `Workshop_SilkTurbineTop`), mapped flat. It has a sunny cream centre that shows through the orb, five brushed swirl arms in aqua, sky blue and lilac, small dandelion seeds blowing round them, and an azure rim band. A smaller copy sits in the tray's dish under the block.
  - **Walkway:** a painted pearl walkway with an azure inlay joins the plinth to the tray.
- **Turbine:** built from a real three-blade turbine:
  - a tapered tubular tower with two azure bands;
  - a streamlined nacelle on a yaw ring, with a tail vane;
  - an azure spinner;
  - three slender blades (`Rotor`) that are narrow at the root, widest near 15 % of their length, taper to the tip and twist 18° → 2°.
- **Orb:** the glass cloud orb with its glow ring (`OrbGlow`), the idle cloud (`OrbCloud`) and the cloud that fills during work (`CloudMass`). Both clouds were remade the same day, when the user asked for a better inside cloud:
  - **Shape:** each is a fluffy cumulus, built from overlapping lobes that a voxel remesh fuses and then smooths, with a flat base (`fluffy_cloud`). They are about 1.2 k and 1.1 k triangles, in place of the decimated cloud mound.
  - **Painted colour:** the vertex colours are painted like a Frutiger cloud. The underside is lilac-blue, the middle white and the sunlit top warm cream, with soft lilac and aqua patches. The alpha carries the glow weight, highest on top.
  - **Shader:** `Workshop_OrbCloud` uses the new `AeroOrbCloud.shader` (*TowerOfBabel/Resources/Orb Cloud*). It gives soft wrap lighting, a silver lining on the rim and a warm inner glow. Each vertex also billows along its normal in three crossed waves; the fastest is under 0.15 Hz, and the base hardly moves.
  - **Silk and seeds:** three soft silk ribbons (`OrbSilk`) and five small seeds (`OrbSeeds`) sink into the idle cloud and turn with it. They replace the earlier wire-like strands.
  - **Clear glass:** the orb now has its own clear glass (`Workshop_OrbGlass`, 20 % opaque). The shared `Workshop_SkyGlass` is tinted blue, and the Sky Block and the plinth's beads and channel still use it; on the orb it washed the cloud out.
- **Silk Clock seeds** use the node's own Dandelion Seeds material. Their `_Release` stays at 0, so they hold their shape:
  - `Feed`: the input, two puffs of parachute seeds;
  - `SeedRing`: a loose ring of ten seeds where the cloud ring was;
  - `SilkSpiral_0/1`: two silk ribbons that spiral down the tower, each carrying seeds (`SilkSeeds_0/1`).
- **Front:** the pearl dish tray on its stem.

**Hand-painted details:** every pearl, azure and blade surface samples `Textures/SilkTurbinePaint.png`, which `paint_silk_turbine.py` paints stroke by stroke with the same bristle brush as the dandelion greens.
- **Pearl:** long strokes run up each part in sky blue, aqua, lilac and sea glass at the foot, then cream, shell, blush and white toward the sunlit top, with short sideways dabs.
- **Azure trim:** strokes run around each band, from navy to bright azure, with cyan glints and a rim light on top.
- **Blades:** brushed lengthwise from frost white at the root to a cyan tip, with a white leading edge.
- **Mapping:** `paint_uv()` wraps each part's atlas region around it, with u around the part (the texture repeats) and v up it. The flat tops of the pad and tray take on a marbled swirl.
- **Materials:** `Workshop_SilkTurbinePaint` is opaque. `Workshop_SilkTurbineBlade` is the same atlas as slightly see-through frosted glass (82 %).

The model is built at 0.75 of the concept scale. `MachineBody` is lower than before (tower top about 1.9 m), so the water press stays the tallest machine.

| Phase | What moves |
| --- | --- |
| **Idle** | The rotor turns slowly (`ProcessorMachineAnimation` idle spin). `AmbientMotion` handles the rest: the seed ring orbits, the silk spirals turn and breathe, the orb cloud spins and bobs with its silk and seeds, and the small clouds drift round the plinth's sky channel. Both spirals turn clockwise seen from above, so their seeds appear to flow down the tower into the orb. The orb clouds billow slowly in their shader. Sky motes and loose parachute seeds drift, and wind streaks blow through the rotor. |
| **Activation (0–30%)** | The orb squashes in anticipation. The two seed puffs rise from in front of the tray into the rotor's wake. They stay pappus-up (`feedTumble` 0; other machines keep the 540° tumble) and shed loose parachutes. |
| **Work (30–70%, then filling to 92%)** | The seeds are drawn down the tower into the orb and shrink away. The rotor winds up to 320°/s, and the seed ring and silk spirals turn faster. Silk is drawn in around the hub and motes swirl in the orb. The orb fills with cloud, and its glow ring and the status lights charge. |
| **Completion** | The rotor coasts down from a burst of speed. The orb pops, and a cloud puff and a ring of seeds blow out from the pad. The output glints and `SkyChime` plays. The Sky Block lifts and slides forward on the dish. |

## Cloud Dandelions: Silk Clock (`SkyEssence.prefab`)

Chosen by the user on 2026-11-15 from [Round02](SKY_ESSENCE_CONCEPTS.md#round02-frutiger-aero-dandelion-designs), with three model requests and two animation requests:
- leaves in different sizes;
- grass in different sizes;
- more hand-painted colours on the green parts;
- each flower picked in turn while its white seeds fly;
- no shaking or rumbling, with motion that flows from flower to flower.

**Model** (`SkyDandelions.fbx`, about 32 k faces; `build_sky_dandelions.py`):
- **Flowers:** `Bloom`, `Stalk_1` and `Stalk_2` keep the earlier bases, heights and leans. They are now siblings under `SkyEssence`, so each can be picked on its own. Each has a painted stem, a receptacle, reflexed bracts and a child `Seeds_<flower>`.
- **Seed clocks:** each `Seeds_<flower>` is a real clock of 84 parachute seeds (aqua achene, thin beak, an umbrella of 10 curled bristles), with its origin at the head centre. Five loose parachute seeds (`Seed_0`–`Seed_4`) float nearby.
- **Rosette:** twelve toothed "lion's tooth" leaves in three sizes. Four are long, four medium, and four young leaves stand up more. It also holds the crown, the closed bud, the yellow bloom, and white and aqua pebbles.
- **Grass:** 520 wind-shader blades in three sizes. Short and medium blades are scattered; tall ones grow in six clumps. Each blade has its own tip colour (lime, yellow-green, emerald, teal, blue-green, gold), and its sway weight is in the vertex alpha.
- **Hand-painted greens:** leaves, stems, bracts, the bud and the crown use `Workshop_DandelionGreen`. It samples `Textures/DandelionGreens.png`, which `paint_dandelion_greens.py` paints stroke by stroke with a bristle brush:
  - warm yellow-green toward the tips and margins, cool emerald and teal toward the base;
  - darker margins, loose chevron veins, a pale midrib with a reddish foot, and pinkish stem feet.
- **Materials:** essence nodes may not use the machine palette, so the pebbles use the new `Workshop_WhitePebble` and `Workshop_AquaPebble` rather than Pearl or Azure. `DandelionSeed.fbx` is one parachute seed for the particle systems.

**Seed shader** (`AeroDandelionSeeds.shader`, *TowerOfBabel/Foliage/Dandelion Seeds*):
- **Seed data:** each vertex knows its seed's umbrella centre. Blender writes it to the UV maps `SeedPivot` and `SeedInfo`, and Unity reads it as `(x, y, -z)`.
- **Release:** the feedback sets one `_Release` value per flower. As it rises, seeds leave in a wave, downwind side first with some randomness. Each seed pops out a little, turns pappus-up, rises, drifts downwind on a one-sided arc, and shrinks away near the end of its flight.
- **Smoothness:** a seed's path is a single smooth curve of its own progress, so nothing oscillates. The silk's emission brightens while it flies.

**Gather, in gather progress p (2.8 s by default):**

| Flower (pick order) | Seeds leave | Seeds land | Bare stalk sinks |
| --- | --- | --- | --- |
| `Stalk_2` (small, left) | 0 – 0.22 | by 0.42 | 0.36 – 0.56 |
| `Stalk_1` (right) | 0.23 – 0.45 | by 0.65 | 0.59 – 0.79 |
| `Bloom` (tall centre, finale) | 0.46 – 0.68 | by 0.88 | 0.82 – 1.02 |

- **Before and while its seeds go:** each flower bows up to 9° downwind; later flowers start a little before their wave. As its stalk sinks to 6 % scale, it bows 10° more.
- **Sound and seeds:** an airy whoosh starts each flower, with the pitch rising from flower to flower. A few loose parachute particles peel off the active head and keep drifting after the essence hides.
- **The loose seeds:** they leave first, downwind, and shrink away by p ≈ 0.42.
- **No kicks:** there is no per-pull kick, scale pulse or shiver. Every curve is eased.
- **Completion:** the picked pose is held for up to 1.5 s until cooldown hides the essence, so it never flashes back during the network round trip. The last head's final seeds lift off as particles.
- **Cancel:** the picture eases back no faster than a gather moves forward (0.35 progress per second), then lands softly. Seeds drift back and stalks rise.
- **Respawn:** flowers regrow over 0.9 s.
- **Idle:** each flower nods on its own slow rhythm (under 0.2 Hz), and the silk shimmers softly. After a reset the nod and the loose seeds' bob fade in rather than jump.
- **One owner:** `AmbientMotion` keeps only the drifting seeds and sparkles; it has no channels on the stalks, so only `EssenceHarvestFeedback` turns them.
- **Gameplay values:** `PresentationScale` reports the share of the dandelions still standing. It follows the gather itself, so a cancel restores it at once.

## Runtime pieces

- **`AmbientMotion`** (new): idle, activity and completion motion layered over a station's work animation.
  - Channels: Spin, Sway, Bob, Breathe and Punch, in station space.
  - Ambient particle systems with idle and work rates. They are started by script, so `playOnAwake` stays off.
  - Gusts, anticipation, and a damped completion kick.
  - Oscillations accumulate their own phase, so a change of speed with activity never makes a part jump. Stroke and completion impulses are eased in rather than applied in one frame.
  - When the station is calm and off screen, the component skips its updates.
  - It reads the station through `IAmbientActivity`: activity, impulse, completion count and dormant state. Both `ProcessorMachineAnimation` and `EssenceHarvestFeedback` implement it.
- **`ProcessorMachineAnimation.Spin.idleSpeed`** (new): parts keep turning at rest and coast after completion. Machines without it are unchanged.
- **`StepEditor(dt)`** (new, editor only): available on all three components. Previews drive the real runtime update in Edit Mode.

## Authoring

1. **Blender:**
   - **Wind Condenser (Silk Turbine):**
     1. Run `python paint_silk_turbine.py`. It paints the atlas, then the plinth's medallion (`SilkTurbineTop.png`). It takes about 2 minutes, and adding the medallion leaves the atlas byte-identical.
     2. Run `build_sky_workshop.py` headless: `blender -b --factory-startup --python build_sky_workshop.py`. Blender 5.3 is at `E:\Steam\steamapps\common\Blender\blender.exe`. Set `SKY_WORKSHOP_REVIEW=1` to also render `_Screenshots-and-previews-to-delete/SilkTurbine/Blender-Review.png`.

     The script uses `sky_concept_kit.py` and `dandelion_kit.py`; its seeds use `parachute()`, whose new optional `width` thickens the bristles. It exports `WindCondenser.fbx` and saves `ArtSource/Resources/Sky_Workshop.blend`.
   - **Dandelions:** run `python paint_dandelion_greens.py` first. Then run `build_sky_dandelions.py`, through the MCP socket or headless with `blender -b --factory-startup --python build_sky_dandelions.py`. It uses `sky_concept_kit.py` and `dandelion_kit.py`, exports `SkyDandelions.fbx` and `DandelionSeed.fbx`, and saves `ArtSource/Resources/Sky_Dandelions.blend`. `REVIEW = True` also renders a Blender still.
2. **Unity:** from the saved, clean Tower scene, run **Tower of Babel → Resources → Apply processor family**, then **Apply essence nodes, blocks and building costs**. New materials: `Workshop_Meadow`, `FrostBlade`, `WindRibbon`, `Daisy`, `Pollen`, `Leaf`, `Stem`. New sound: `SkyChime`. Since the Silk Turbine, **Apply processor family** also:
   - imports the painted atlas (repeating in u);
   - creates `Workshop_SilkTurbinePaint`, `Workshop_SilkTurbineBlade`, `Workshop_SilkTurbineTop` (the medallion, clamped), `Workshop_OrbCloud` (with its glow, rim and billow values) and `Workshop_OrbGlass`;
   - imports `DandelionSeed.fbx` for the seed particles;
   - creates the seed material if the essence tool has not.

   A condenser rework needs only this step.
3. **Fix:** `ResourceStationKit.SceneRoot` now finds station instances inside the scene's **Resources** and **Processors** groups. Before, every Apply run added a root-level duplicate of each machine and essence node. The duplicates made by this session's first run were removed.

**Motion review:**
- `SkyWorkshopPreview.Capture("PassN")` renders frames in Edit Mode, driving the real runtime calls.
- `python Tools/BuildingAero/assemble_sky_workshop_preview.py PassN` makes a side-by-side GIF and a key-frame sheet.
- The output goes to `_Screenshots-and-previews-to-delete/SkyWorkshop/PassN/`.

## Review passes

- **Pass 1** found five problems:
  - The entering essence was too small to follow.
  - The gust ring was invisible.
  - The tray read as a dark floating ring.
  - The dandelions looked crushed when gathered.
  - The seed heads looked spiky.
- **Pass 2** fixed them:
  - The essence is larger and leaves a cloud trail.
  - The gust ring is made of cloud puffs.
  - The tray is a raised pearl dish with a foot.
  - The dandelions shrink whole and lean downwind.
  - The seed heads have fuller cloud cores and more, softer seeds.
  - The review camera is closer.
- **Pass 3** answered user feedback that both animations shook too much. The causes, and the fixes:
  - **Phase jumps (both stations).** Sway, bob and breathe were `sin(time × rate × (1 + activity))`. Time includes a per-station seed of up to 100 s, so every change of activity shifted the phase by tens of radians, and parts jerked when work started and stopped. Each channel now accumulates its own phase.
  - **Shivers.** Strokes and completions added a 5 Hz sway shiver to the daisies and stalks, and the dandelion body had a 6 Hz, ±5° shiver plus a 1 Hz wobble while gathering. These were replaced by an eased downwind bow.
  - **Completion spring.** It was fast (17 rad/s) and multiplied 2.2× on sway, so daisies whipped about 24°. It is now one soft overshoot (7.5 rad/s, multiplied 0.8× on sway) that fades to exactly zero.
  - **Speed jumps.** On completion, the spin speeds doubled in one frame. The whirl now winds up over 0.25 s, and the ambient spin boost is eased in.
  - **Snaps.** A cancelled gather snapped the dandelions upright and full size, and the loose seeds back into place. They now ease back.
  - **Calmer values.** Rotor 560 → 320°/s, idle 38 → 30°/s. Cloud ring 80 → 42°/s and wind ribbons 210 → 95°/s at full work. Ribbon breathing is halved and slowed. Daisies sway 3–5° at about 0.15 Hz (was 5–11°). Side stalks sway 2.5–5°. The essence feed's swirl is reduced.
  - **Preview.** The review is now rendered at 30 fps (`Capture("Pass3", 30, 7.6f, 540)`), so the 12 fps strobing no longer hides how the motion looks.
- **Pass 4 (2026-11-15, Silk Clock)** reviewed the flower-by-flower gather at 30 fps (`Capture("Pass4", 30, 7.6f, 540)`). The dandelion-only key frames and GIF are `Dandelions_Pass4_Keyframes.png` and `Dandelions_Pass4.gif` in `_Screenshots-and-previews-to-delete/SkyWorkshop/Pass4/`. Fixes during the pass:
  - The flying achenes read as dark dashes, so they were lightened and the silk thickened (2.4 mm bristles) and brightened, with more glow in flight.
  - A completed gather briefly rewound before cooldown hid it; it is now held.
  - A cancel rewound twice as fast as the gather; it is now capped.
  - The first frame after a reset jumped to the idle nod and bob, and the first flower carried a small bow at rest. Both now start from zero.
- **Pass 5 (2026-11-15, Silk Turbine)** reviewed the rebuilt condenser at 30 fps (`Capture("Pass5", 30, 7.6f, 540)`, then `assemble_sky_workshop_preview.py Pass5 30`). Without the `30`, the sheet labels frames at 12 fps and shows the wrong moments.
  - **Close-ups:** `_Screenshots-and-previews-to-delete/SilkTurbine/Unity-Turbine.png`, `Unity-CloseUp-Nacelle.png` and `Unity-CloseUp-Pad.png` show the brushwork in Unity.
  - **Fix during the pass:** the first atlas's pearl strokes were too faint to read as painting (47 distinct shades on the test grid). They are now stronger and more varied in hue (106 shades).
- **Pass 6 (2026-11-15, plinth and orb cloud)** reviewed the enhanced base and the new cloud (`Capture("Pass6", 30, 7.6f, 540)`, `assemble_sky_workshop_preview.py Pass6 30`), with close-ups `Unity-CloseUp-Plinth.png` and `Unity-CloseUp-OrbCloud.png` in `_Screenshots-and-previews-to-delete/SilkTurbine/`. Fixes during the pass:
  - **Faces:** the first plinth and clouds were 35.7 k faces. Subdivided 96-segment lathes and dense medallion grids caused most of it, and the clouds were decimated to twice their intended triangles. They were trimmed back to about 16 k exported.
  - **Washed-out cloud:** through the shared blue `SkyGlass` (56 % opaque), the cloud read flat and grey-blue. The orb got its own clear glass, and the cloud's glow and rim were raised. `CreateSilkTurbineMaterials` sets these values explicitly, because an existing material keeps its saved values when a shader's defaults change.
  - **First render after Apply:** the first preview render right after Apply missed the cloud once, while every later render showed it. It was a first-use effect of the freshly created shader and material, not a fault in the model.
- **Remaining notes:**
  - The gust ring reads best in motion, not in stills.

## Verification

- **EditMode `SkyWorkshopEditModeTests`** (10 tests).
  - **The plinth:**
    - it uses the painted atlas, the medallion and sky glass;
    - its foot is scalloped into petals: round the back half, the outer edge varies by more than 1.5 cm with the angle;
    - the medallion has a warm centre, an azure rim band and swirl arms crossing its middle;
    - the channel clouds ride the channel, stay low and drift at rest.
  - **The orb clouds:**
    - both use the Orb Cloud shader with a small billow whose fastest wave is under 0.6 Hz;
    - they are smooth-shaded lobed cumulus meshes with painted vertex colours: brighter on top and cooler underneath, the top glowing most, on a flat base;
    - they stay inside the orb, behind its clear glass;
    - `OrbSilk` is soft ribbon, and `OrbSeeds` turn with the cloud.

  For the Silk Turbine (2026-11-15):
  - **The model:**
    - every turbine part is present;
    - no hill, grass, daisy, cloud ring or wind ribbon is left, and no meadow, grass, daisy or pollen material;
    - the pad is low;
    - the pad, body and rotor use the painted atlas across their region, and the blades use the frosted painted glass.
  - **The atlas** (read from the PNG):
    - the pearl has cool and warm strokes and more than 60 distinct shades;
    - the trim's shadow and highlight strokes are more than a quarter of full brightness apart, with cyan glints;
    - the blades fade from frost white to a cyan tip.
  - **The seeds:** they use the node's seed shader with pivots and vertex colours; seeds ride the silk spirals, and silk knits into the orb cloud.
  - **Idle life:**
    - the rotor's idle spin and the ambient rates;
    - parachute-seed drift;
    - the seed ring orbits, the silk turns and breathes, and both spirals turn clockwise.
  - **The input:** the seeds stay within 1° of upright on their way in, rise to the hub, then are drawn into the orb. The feed trail is parachute seeds.
  - **Work:** the seed ring speeds up, the orb squashes and pops, and the effect lists include the ring of seeds and the chime.
  - **Smoothness**, stepped at 60 fps through a whole cycle:
    - the silk's breath changes scale by less than 1 % a frame;
    - spinning parts change speed by less than 1° per frame;
    - the orb changes scale by less than 1.2 % a frame.
  - Silk Clock model (2026-11-15):
    - the pick order, sibling stalks, painted stems and seed clocks on the seed shader;
    - every bristle starting at its seed's stored pivot;
    - the atlas mixing yellow-green, green and teal-green;
    - the grass in short, medium and tall sizes with at least 8 tip shades;
    - no ambient channels on the stalks, and parachute-seed particles.
  - Picking the flowers one after another:
    - at p 0.12 only the first flower releases, and it bows downwind;
    - at p 0.36 the first flower's seeds are all gone and the second's are flying;
    - at p 0.62 the first stalk has sunk and the centre flower has started;
    - at p 1 everything is picked and flown;
    - idle nod, dormant on cooldown, and a clean reset.
  - Silk Clock smoothness, stepped at 60 fps through a gather, the completion hold, cooldown, respawn and a cancelled gather:
    - stalks turn less than 0.8° per frame and change scale by less than 7 % of rest per frame;
    - visible release moves less than 0.08 per frame, and loose seeds less than 5 cm per frame;
    - the completion pose never rewinds before cooldown, and the cancel eases fully back.
- **`ProcessorFamilyEditModeTests`** (10) were updated for the Wind Condenser: a rotor turning about the front axis, and no `SkySpinner` left anywhere. Since the Silk Turbine, its base is the painted pad (`Workshop_SilkTurbinePaint`) with the green lamps. These tests also cover the new `feedTumble` field: every other machine keeps its 540° tumble.
- **`EssenceWorkshopEditModeTests`** (8) were updated for the `Rosette` base.
- **`IronWorkshopEditModeTests`** (8) pass again now that the duplicates are gone.
- **PlayMode `ResourceProcessorPlayModeTests`** covers the host gather → condense flow for the sky chain through the automated runner.

## Sources

- [Juice it or lose it (Jonasson and Purho), resource page](https://gamejuice.pages.dev/resources/juice-it-or-lose-it) and [talk](https://www.youtube.com/watch/Fy0aCDmgnxg)
- [Squeezing more juice out of your game design](https://gamedeveloper.com/design/squeezing-more-juice-out-of-your-game-design-)
- [Juicy game feedback survey (arXiv 2011.09201)](https://arxiv.org/pdf/2011.09201)
- [Game feel: why juice matters](https://bugnet.io/blog/game-feel-why-juice-matters-and-how-to-add-it)
- [Game feel, hit-stop, screen shake and easing](https://appscale.blog/ru/blog/game-feel-juice-hitstop-screenshake-easing-2026)
- [When juice becomes a crutch](https://www.wayline.io/blog/the-seductive-squeeze-when-juice-in-game-development-becomes-a-crutch)
- [Juiciness and World of Goo](https://mechanicsofmagic.com/2025/04/22/juiciness-and-world-of-goo-justin/)
- [Idle-game reward feel and pity timers](https://lostrabbitdigital.pages.dev/blog/starbrew-dopamine-loop-pity-timers.html)
