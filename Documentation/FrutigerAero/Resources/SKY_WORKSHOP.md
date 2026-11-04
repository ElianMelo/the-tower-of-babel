# Sky workshop: Wind Condenser and Cloud Dandelions

Requested on 2026-11-04. The user chose two concepts to replace the old sky essence pair:
- **Wind Condenser** replaces the Sky spinner. It comes from variant 03A (Bliss Hill) in [sky processor concepts](SKY_PROCESSOR_CONCEPTS.md).
- **Cloud Dandelions** replace the fallen cloud (`SkyCloud`). They come from concept 03 in [sky essence concepts](SKY_ESSENCE_CONCEPTS.md).

Both models were enhanced, given idle and activation motion, effects and particles, then reviewed and improved in a second pass. A third pass, after user feedback that both animations shook too much, made all motion smooth and calmer (see [review passes](#review-passes)).

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
| **The world is alive at rest.** Idle stations should invite interaction and never look frozen. | The rotor turns in the breeze at 30°/s. A ring of cloud orbits the tower, wind ribbons spiral up it, the cloud in the orb drifts, daisies nod and the grass sways in the wind shader. The dandelions nod out of step with each other, seeds drift off downwind and sparkles rise. |
| **Wind is uneven.** Constant motion reads as mechanical. | A slow layered-noise gust occasionally strengthens spin, sway and particle rates. Each placed station has its own rhythm, so a row never moves in lockstep. |
| **Anticipation, action, follow-through.** | When work starts, the orb squashes slightly over 0.5 s. Work speeds up quickly, then eases back slowly afterwards (0.3 s rise, 1.2 s settle). After completion, the rotor winds up into a brief whirl over 0.25 s, then coasts back to idle. |
| **Escalation tracks progress.** The build-up makes you anticipate the reward. | Rotor speed (35% → 100%), particle rates, the orb glow ring, the status lights (lit in sequence), the hum pitch and the pitch of each gather pull all rise with progress. |
| **Clear cause and effect.** | The essence is visible on its way in. A cloud tuft with a cloud trail is swept up from the player's side into the rotor, then drawn down the tower into the orb, which visibly fills with cloud. |
| **A short, clear reward peak.** | On completion, the orb pops past its rest size and settles with one soft overshoot (a slow, well-damped spring). A puff of cloud and a gust ring roll over the grass, glints play and a bright rising three-note chime sounds (`SkyChime`). The Sky Block appears on the tray only after the server confirms the exchange. |
| **Proportion and restraint.** Routine actions repeat hundreds of times in an incremental game. | There is no screen shake, hit-stop or vibration. Every motion is a slow, continuous curve: no oscillation runs faster than about 0.6 Hz, and impulses are eased in. Idle motion is subtle, work is medium, and the peak is clear but short (1.1 s). Particle systems are capped at 64 and emit nothing at idle unless they are ambient. |
| **Natural, not mechanical, gathering.** | The dandelions are not squashed. Each pull bows the stalks a little further downwind, like a gust, and throws seeds. The stalks shrink back whole and lean downwind as the seeds blow away. The leaf rosette, the yellow bloom and the bud stay through cooldown. |

## Wind Condenser (`WindCondenser.prefab`)

**Model** (`WindCondenser.fbx`, about 17 k faces):
- **Hill:** a glossy rolling hill (`Workshop_Meadow`) with about 470 grass blades. The blades use the wind shader: albedo is in the vertex colour, and the sway weight is in the vertex alpha.
- **Detail:** eight daisies, a pebble path to the tray, and dew bubbles.
- **Orb:** a glass cloud orb on a pearl seat, with a glow ring (`OrbGlow`). Inside it are an idle cloud (`OrbCloud`) and the cloud that fills during work (`CloudMass`).
- **Tower:** a pearl tower with azure bands, a nacelle with a tail vane, and a three-blade frosted rotor (`Rotor`).
- **Around the tower:** an orbiting cloud ring and two wind ribbons.
- **Front:** a pearl dish tray and three green status lights.

The model is built at 0.75 of the concept scale, so it matches the family footprint and the water press stays the tallest machine.

| Phase | What moves |
| --- | --- |
| **Idle** | The rotor turns slowly (`ProcessorMachineAnimation` idle spin). `AmbientMotion` handles the rest: the cloud ring orbits, the wind ribbons turn and breathe, the orb cloud spins and bobs, and the daisies nod with gusts. Sky motes and pollen drift, and wind streaks blow through the rotor. |
| **Activation (0–30%)** | The orb squashes in anticipation. The essence tuft and its cloud trail arc from the player into the hub. |
| **Work (30–70%, then filling to 92%)** | The tuft is drawn down the tower into the orb and shrinks away. The rotor winds up to 320°/s and the cloud ring and ribbons turn faster. Cloud is sucked in around the rotor and motes swirl in the orb. The orb fills with cloud, and its glow ring and the status lights charge. |
| **Completion** | The rotor coasts down from a burst of speed. The orb pops, and a cloud puff and gust ring burst out. The output glints and `SkyChime` plays. The Sky Block lifts and slides forward on the dish. |

## Cloud Dandelions (`SkyEssence.prefab`, `SkyDandelions.fbx`, about 12.7 k faces)

- **Essence (hides on cooldown):** `Bloom` is the centre stalk; the side stalks `Stalk_1` and `Stalk_2` are its children. Each head has a cloud core, 24–30 pappus seeds and a glowing heart. Five loose cloud seeds (`Seed_0`–`Seed_4`) drift nearby.
- **Base (stays):** `Rosette` holds nine glossy leaves with ribs, a crown, a bud, a yellow bloom and river pebbles. The `Grass` ring uses the wind shader.
- **Feedback:** `EssenceHarvestFeedback` uses the new style `SkyBloom`. The value is appended to the enum, so saved values don't change.
  - **Idle:** the centre stalk nods.
  - **Each of the six pulls:** an eased 2.5° gust bow, a seed burst, motes and a rising pitch.
  - **Gathering:** the stalks shrink whole to half size and bow up to 24° downwind. If the gather is cancelled they ease back upright over about half a second instead of snapping.
  - **Completion:** a seed burst, then the essence hides.
  - **Respawn:** the stalks regrow.
- **`AmbientMotion`:** sways the side stalks and runs the ambient drifting seeds and sky sparkles. Their rates rise while gathering, and they stop while the node is on cooldown (`Dormant`).

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

1. **Blender:** send `Tools/BuildingAero/build_sky_workshop.py` to the Blender MCP socket. It uses `sky_concept_kit.py` and exports both FBX files. The source is `ArtSource/Resources/Sky_Workshop.blend`.
2. **Unity:** from the saved, clean Tower scene, run **Tower of Babel → Resources → Apply processor family**, then **Apply essence nodes, blocks and building costs**. New materials: `Workshop_Meadow`, `FrostBlade`, `WindRibbon`, `Daisy`, `Pollen`, `Leaf`, `Stem`. New sound: `SkyChime`.
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
- **Remaining notes:**
  - The gust ring reads best in motion, not in stills.

## Verification

- **EditMode `SkyWorkshopEditModeTests`** (5 tests):
  - the condenser's parts and idle life: idle spin, cloud ring orbit, daisy nod, ambient rates;
  - work speeding the wind, the anticipation squash, the completion pop and settle, the effect lists and the chime;
  - the dandelions: style, hierarchy, ambient seeds, nodding and swaying, shrinking whole and leaning when gathered, dormant on cooldown;
  - smoothness (Pass 3): each station is stepped at 60 fps through a whole cycle. Daisies may not move more than 1° in a frame, and spinning parts may not change their speed by more than 1° per frame. The orb may not change scale by more than 1.2% in a frame. The dandelion stalks may not turn more than 2° per frame through six pulls and a cancelled gather, or change scale by more than 7%.
- **`ProcessorFamilyEditModeTests`** (10) were updated for the Wind Condenser: the meadow base, a rotor turning about the front axis, and no `SkySpinner` left anywhere.
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
