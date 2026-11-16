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

## Round 03: Silk Clock processor propositions

Requested on 2026-11-15, after the sky essence became the Silk Clock dandelion ([sky essence concepts, Round02](SKY_ESSENCE_CONCEPTS.md#round02-frutiger-aero-dandelion-designs)). The user asked for 10 propositions to rework the Wind Condenser on the same principle, respecting the design. The user then asked to see all ten before deciding, so each one has a static concept render (see [authoring](#round-03-authoring)). Nothing is animated or imported into Unity yet.

### The principle carried over from the Silk Clock

The Silk Clock rework did five things. Every proposition applies all five to the processor:

1. **Real reference first, Frutiger finish second.** The dandelion was rebuilt from a photo of a real clock, then given silky pappus and glossy aqua achenes. Each proposition names the real machine or object it is built from and keeps that object's true proportions and working parts. The pearl, azure, frosted glass and soft glow come after.
2. **Keep the contract, so the motion still fits.** The dandelion kept its hierarchy, pivots and gameplay values. The processor keeps everything listed under [what all ten keep](#what-all-ten-keep).
3. **One part at a time, flowing.** The flowers are picked in turn, and each one hands on to the next. In the processor, the work moves through its parts in a clear order: seed by seed, cup by cup, or sail by sail. There are no kicks, pulses or shivers.
4. **The real essence, all the way.** The gathered essence is now parachute seeds, but the condenser still takes in a cloud tuft (`Feed` is a `SkyCloud` tuft). In all ten, the two sky essence enter as Silk Clock seeds, and the process turns their silk into the cloud sealed in the Sky Block. The chain reads: seeds → silk → cloud → Sky Block.
5. **A hand-painted meadow that matches its node.** The base uses the dandelion greens (`Workshop_DandelionGreen` and its painted atlas), grass in three sizes with its own tip shade per blade, and the white and aqua pebbles. The processor and its essence node then look like one meadow.

### What all ten keep

- **Gameplay:**
  - 2 sky essence → 1 Sky Block in 3.5 s;
  - processor ID `tower-sky-spinner-01`;
  - the `WindCondenser.prefab` asset and its GUID;
  - its place at x 21.6 in the workshop row.
- **Family layout:** three status lights and a front `BlockSocket`. The tray is empty at idle, and the Sky Block (`SkyBlock.fbx`) appears only after the server confirms the exchange.
- **Family identity:**
  - The process kind is Spin: something turns, and it is the hero.
  - Each design stays different from the other processors: no water wheel like the river mill, no dome like the planter, and no piston column like the press.
  - The water press stays the tallest machine.
- **Feed rules:** the input starts in front of the machine, on the player's side. It is inside by 30% of the cycle and consumed by 88%.
- **Motion rules ([game feel](SKY_WORKSHOP.md#game-feel-what-the-research-says-and-how-it-is-applied)):**
  - alive at rest, with uneven gusts;
  - nothing faster than about 0.6 Hz, and every impulse eased in;
  - one soft overshoot on completion, then a pose held until the output shows;
  - a cancel that eases back no faster than the work moved forward;
  - one owner per transform.
- **Budgets:** colliders stay fixed. Particle systems are capped at 64 and emit nothing at idle unless they are ambient. The model stays near the current 17 k faces or below.

### What all ten change

- **Input:** the two essence arrive one after the other, at about 0–0.16 and 0.12–0.28 of the cycle. Each is a small puff of parachute seeds (`DandelionSeed.fbx`), pappus up, on a one-sided downwind arc, the way the seeds left the flower. They replace the cloud tuft and its cloud trail.
- **Silk becomes cloud:** inside the machine, the white pappus visibly gathers into a soft cloud. That cloud is the one sealed in the Sky Block.
- **Base:**
  - The Bliss hill keeps its glossy rolling shape but is repainted with the dandelion greens.
  - The daisies become two or three young dandelion rosettes with yellow blooms and closed buds. They have no seed clocks, so the essence node stays the only place seeds grow.
  - The pebbles are the white and aqua ones.
- **Ambient:** where a design keeps the orbiting cloud ring, it becomes a loose ring of drifting parachute seeds. Pollen and motes stay.

| # | Proposition | Real reference | What spins | How seeds become the Sky Block |
| --- | --- | --- | --- | --- |
| 01 | Silk Turbine | A modern three-blade wind turbine | The rotor | Seeds caught in the rotor's wake spiral down the tower as a silk ribbon and knit into cloud in the orb |
| 02 | Pinwheel Trio | Garden pinwheels | Three sibling pinwheels, in the node's pick order | Each pinwheel spins up with its share of the seeds and hands the silk to the next; the tall centre one spins it into cloud |
| 03 | Pappus Rotor | A dandelion seed in flight and the vortex ring above it | A giant glass pappus umbrella | Seeds are drawn into a rolling cloud ring above the umbrella, which sinks down the mast into the orb |
| 04 | Cup Anemometer | A weather-mast cup anemometer and wind vane | Three glass cups | Each cup scoops a puff of seeds in turn; the spin slings the silk down into a glass column as cloud |
| 05 | Silk Spinning Wheel | A Saxony spinning wheel | A wind-driven drive wheel and the flyer | The pappus is drafted seed by seed into one thread that winds onto a glass bobbin as a cloud-white cop |
| 06 | Floss Spinner | A candy-floss machine | The spinner head | The head flings the silk out as threads that web across a bowl; a glass wand winds the web into a cloud |
| 07 | Silk Sail Mill | A Dutch smock windmill | Four lattice sails | The bare sails are dressed with silk one by one; the full sails mill it into cloud down a chute |
| 08 | Clock Rebuilder | The dandelion head itself | The rebuilt seed head | Seeds dock one by one on a glass receptacle in golden-spiral order; the full clock turns and draws its silk into a cloud core |
| 09 | Silk Top | A large spinning top | The top | Seeds wind onto the top's shoulder as a silk band, which lifts off as a rolling cloud ring |
| 10 | Kite Winch | A kite-power ground station and kite reel | The winch drum | Seeds climb the tether to a silk kite that gathers a cloud; the drum reels the kite in and the cloud is folded into the block |

### 01 Silk Turbine: the realistic baseline

- **Reference:** a modern horizontal-axis turbine. It has a tapered tubular tower, a streamlined nacelle on a yaw ring, a spinner nose cone, and three long, slender, twisted airfoil blades.
- **Model:**
  - The current layout with real proportions: a thinner tapered pearl tower, and longer, slimmer frosted blades with twist and a root-to-tip taper.
  - An azure spinner cone.
  - The glass orb, seat, collar and pearl dish tray stay.
- **Finish:** a clear-coated pearl tower, frosted blades with a pale cyan glow at the tips, and the orb's glow ring.
- **Process:**
  1. The seeds rise from the player's side into the rotor's wake (0–0.28).
  2. The rotor winds up from 35% to 100%.
  3. The seeds stream down the tower one after another in a spiral silk ribbon (0.28–0.6).
  4. In the orb, the pappus gathers and knits into cloud (the `CloudMass` fill, 0.32–0.92).
  5. Completion pops the orb and sends a gust ring of seeds over the grass. The block slides onto the dish.
- **Idle:** the rotor turns at 30°/s. A loose ring of seeds orbits where the cloud ring was, and the young dandelions nod.
- **Reuse and risk:**
  - This is the lowest risk. It keeps every object name (`Rotor`, `Orb`, `CloudMass`, `Feed`) and the current channels, so it is mostly a model, material and particle pass.
  - It is also the least new: it still reads as a turbine standing next to a dandelion.

### 02 Pinwheel Trio: the flower-by-flower machine

- **Reference:** classic garden pinwheels: four vanes folded from a square sheet to a hub, on a stick.
- **Model:**
  - Three pinwheels on slim pearl stalks at the Silk Clock's stalk heights and leans: a small one on the left, one on the right and a tall one in the centre.
  - Each pinwheel is an empty at its base holding `Stick` and `Wheel`, like the dandelion stalks.
  - A small glass bowl under the centre pinwheel collects the cloud. The tray stands in front.
- **Finish:** thin-film glass vanes in cyan, mint and white, pastel-iridescent at the edges. Azure hubs, each with a soft glow ring.
- **Process,** in the node's pick order:
  1. The seeds arrive and catch on the small left pinwheel (0–0.3). It spins up as its vanes fill with silk, then eases down while a silk ribbon carries the seeds to the right one (0.3–0.5).
  2. The right pinwheel spins and passes the silk on (0.5–0.8).
  3. The tall centre pinwheel's whirl is the finale: it spins the silk into a cloud in the bowl (0.75–0.95).
  4. Each status light sits at the foot of its pinwheel and lights when that wheel finishes.
- **Idle:** each pinwheel turns lazily on its own rhythm, out of step with the others, and leans into gusts.
- **Reuse and risk:**
  - The gather's pick order, stagger and lean values carry over directly, so gathering and processing read as one rhythm.
  - Each wheel needs its own Spin channel that runs over part of the cycle. This is a small addition to `ProcessorMachineAnimation`.
  - It has the lowest silhouette of the ten, so the pinwheels must stay large enough to read at game distance.

### 03 Pappus Rotor: the seed's own flight

- **Reference:** a single dandelion seed in flight. Its bristly pappus holds a stable, separated vortex ring of air just above it, and that ring keeps the seed aloft (Cummins et al., *Nature* 562, 2018).
- **Model:**
  - A pearl mast rises from the orb.
  - On top, a giant frosted-glass pappus umbrella (about 60 bristle cards around a pearl hub) turns slowly about the vertical.
  - Above the umbrella hovers the vortex ring: a torus of soft cloud that rolls on itself.
- **Finish:** frosted glass bristles with white silk tips and a back-lit glow, the rolling ring in a soft cloud shader, and the glass orb below.
- **Process:**
  1. The seeds rise and are drawn into the ring one by one (0–0.3).
  2. The ring thickens and rolls faster as it gathers silk (0.3–0.7).
  3. It sinks through the umbrella, down the mast and into the orb as cloud (0.7–0.92).
  4. On completion, a fresh small ring lifts off the umbrella like a smoke ring.
- **Idle:** the umbrella turns very slowly, and a faint ring rolls above it.
- **Reuse and risk:**
  - It has a striking, original parasol silhouette that is literally the seed, and the science is real.
  - It needs a rolling-ring shader (UV scrolled around the torus) and careful scale, so the umbrella does not read as a lamp.

### 04 Cup Anemometer: cup by cup

- **Reference:** a meteorological cup anemometer and wind vane on a weather mast: three hemispherical cups on arms, turning about a vertical axis.
- **Model:**
  - A short pearl mast on the hill carries three large glass cups on chrome arms.
  - Under the cups, a glass column with a pearl cap holds the cloud.
  - A wind vane above it has a parachute-seed tail and always points downwind, the side the node's seeds leave from.
  - The tray stands on its stem in front.
- **Finish:** clear glass cups with azure rims, chrome arms, a glowing hub and a pearl vane.
- **Process:**
  1. The cups turn past the intake. Each scoops a puff of seeds in turn (the first by 0.16, the second by 0.28) and glows as it fills. The third cup catches the stragglers by 0.4.
  2. The cups spin up, and the silk is slung inward and down the mast into the column, where it gathers as cloud (0.4–0.9).
- **Idle:** the cups turn with the wind, slowing and speeding with gusts. The vane swings gently toward each gust.
- **Reuse and risk:**
  - It is short and wide, and clearly not a turbine.
  - The cup-by-cup read needs big cups (about 0.25 m) at a camera-friendly height.

### 05 Silk Spinning Wheel: drafted into a thread

- **Reference:** a Saxony spinning wheel: a large drive wheel, a drive band, the flyer and bobbin, and a distaff holding fibre.
- **Model:**
  - A slanted pearl frame on the hill.
  - A spoked frosted-glass drive wheel with an azure rim. Its spokes are small frosted sails, so the breeze turns it instead of a treadle.
  - A glass distaff where the seeds land, then the flyer and a glass bobbin.
  - The tray at the front of the bench.
- **Finish:** pearl and frosted glass, a single bright silk thread with a soft glow, and a cloud-white cop on the bobbin.
- **Process:**
  1. The seeds float onto the distaff (0–0.3).
  2. The pappus is drafted, seed by seed, into one continuous thread that runs to the flyer (0.3–0.75).
  3. The flyer whirls, and the bobbin fills with a cloud-white cop (a `Fill` channel).
  4. On completion, the cop puffs into a cloud that is sealed into the Sky Block on the tray.
- **Idle:** the wheel rocks slowly in the breeze, and the flyer turns a little now and then. A few loose fibres drift from the distaff.
- **Reuse and risk:**
  - It is the most literal "spin", with a warm, handmade read.
  - A large upright wheel can echo the river mill's water wheel; the slanted frame, glass sail spokes and distaff keep it apart.
  - The thread needs a scrolling shader.

### 06 Floss Spinner: silk flung into cloud

- **Reference:** a candy-floss machine. A small spinner head in the middle of a wide bowl flings fine threads, which web across the bowl and are wound up on a stick.
- **Model:**
  - A wide pearl bowl on a short pedestal, with a clear glass splash ring.
  - An azure spinner head in the centre.
  - A glass wand on a pearl arm that dips in and winds the floss.
- **Finish:** a glossy pearl bowl with a pale cyan inner glaze, a translucent candy-blue spinner and white glowing floss.
- **Process:**
  1. The seeds drop into the spinning head (0–0.3).
  2. The head spins up and flings their silk outward as fine threads, which web across the bowl in an outward spiral (0.3–0.65).
  3. The wand lowers and turns, winding the web into a cloud puff (0.6–0.9).
  4. The puff settles into the Sky Block on the tray.
- **Idle:** the head turns slowly, and a few wisps of floss drift up from the bowl.
- **Reuse and risk:**
  - It tells the clearest "silk becomes cloud" story, with a strong, low, wide silhouette.
  - It reads as a fairground machine rather than nature; the meadow base and pearl finish keep it in the family.
  - The web needs a growing-radius shader or a few layered cards.

### 07 Silk Sail Mill: sail by sail

- **Reference:** a Dutch smock windmill: a tapered tower, a cap turned to the wind by a fantail, and four lattice sails that the miller dresses with sailcloth.
- **Model:**
  - A short tapered pearl smock tower with azure trim.
  - A rounded cap with a small fantail.
  - Four lattice sails.
  - A small pearl chute at the front that delivers onto the tray.
- **Finish:** a pearl tower, frosted-glass lattices, silk sailcloth with a soft back-light and an azure cap.
- **Process:**
  1. At idle the lattices are bare, like picked stalks. As the seeds arrive, the cloths fill with silk sail by sail, and each filled sail adds speed (0–0.4).
  2. The four full sails turn, and the silk is milled into cloud (0.4–0.8).
  3. The cloud flows down the chute (0.6–0.9).
  4. On completion, the cloths furl back one after another, and the mill coasts.
- **Idle:** the bare lattice turns slowly, and the fantail keeps the cap facing the wind.
- **Reuse and risk:**
  - It has a classic, readable silhouette and a clear sequential fill.
  - The word "mill" may blur it with the river mill, but the windmill shape and the sails keep it distinct.
  - The sails need a fill value per sail, like the per-flower `_Release`.

### 08 Clock Rebuilder: the clock, put back together

- **Reference:** the dandelion head itself: a domed receptacle with its achenes on a golden spiral, the same structure as the Silk Clock.
- **Model:**
  - A pearl machine stalk with azure bands holds a large glass receptacle.
  - The receptacle's surface is pitted with about 84 empty sockets on the golden spiral, like the spent head in the reference photo.
  - A pearl collar below the receptacle; the tray in front.
- **Finish:** a clear glass receptacle with a soft cyan core, a pearl stalk, and the seed shader's silk and aqua achenes.
- **Process:**
  1. The seeds fly in and dock one by one, in spiral order, rebuilding a full clock (0–0.45). This is the Silk Clock release run backwards: the same seed shader, with each seed flying from outside onto its stored pivot.
  2. The full head turns once on its stalk (0.45–0.7).
  3. The silk draws inward and becomes the cloud core (0.6–0.88), and the cloud drops into the Sky Block.
  4. Completion leaves the bare glass receptacle, ready for the next run.
- **Idle:** the empty receptacle turns very slowly and glints.
- **Reuse and risk:**
  - It reuses the most of the new technology (the seed shader, its pivots and `dandelion_kit.py`), and it closes the loop: the player picks a clock, and the machine rebuilds one.
  - Its silhouette is the closest to the essence node. The pearl and azure machine stalk and the glass head must keep it from reading as a fourth dandelion.

### 09 Silk Top: a band of silk, released as a ring

- **Reference:** a large spinning top, a gyroscopic top on a dish.
- **Model:**
  - A fat glass top with a pearl shoulder and an azure tip spins on a shallow pearl dish set into the hill.
  - Small frosted vanes on its crown catch the breeze.
  - The tray stands in front.
- **Finish:** a clear glass body with a cloud core, a pearl shoulder band, and a soft spin-blur shader on the glass.
- **Process:**
  1. The seeds circle in and wind onto the top's shoulder as a silk band, one turn per puff (0–0.4).
  2. The top spins up with a slow, calm precession, under 0.3 Hz (0.3–0.6).
  3. The silk band lifts off as a rolling cloud ring that settles onto the tray and seals into the Sky Block (0.65–0.9).
  4. The top slows down and stays upright; it never wobbles toward a fall.
- **Idle:** the top turns slowly on its dish.
- **Reuse and risk:**
  - It has a simple, unusual silhouette with one hero part.
  - A top must never look about to fall, so the precession stays slight and eased.

### 10 Kite Winch: reeling in the sky

- **Reference:** the ground station of a kite-power system. The kite pulls line off a drum, then the drum winds it back in. A classic kite reel works the same way.
- **Model:**
  - A pearl winch drum with azure flanges on the hill, and a pulley post.
  - A silk kite on a thin tether: a pale, almost translucent delta kite with a pappus-bristle tail.
- **Finish:** a back-lit silk kite, a glossy pearl drum and a glowing tether.
- **Process:**
  1. The seeds catch on the tether and climb it, bead by bead, to the kite (0–0.3).
  2. The kite gathers a cloud on its sail (0.3–0.6).
  3. The drum spins and reels the kite down (0.55–0.85).
  4. The cloud is folded off the kite into the Sky Block on the tray.
  5. After completion, the kite climbs back to its idle height, eased.
- **Idle:** the kite drifts in slow, wide figure-eights (under 0.2 Hz), and the tether sways.
- **Reuse and risk:**
  - It is the most "sky" of the ten, with a fresh silhouette.
  - Height is the main risk. A high kite breaks the family rule that the water press is the tallest machine, and the `ProcessorFamilyEditModeTests` check for it. The kite must either fly low, or the rule must be revisited.
  - The tether needs a curve, from a line renderer or a bent mesh.

### Round 03 authoring

The concepts are built at the condenser's concept scale; production is 0.75 of it. They have 22–33 k faces each, which is concept density.

- **Blender:** `Tools/BuildingAero/build_sky_silk_processor_concepts.py` builds the scene `Aero_SkyProcessorSilkConcepts`. It uses:
  - `sky_concept_kit.py` for the materials, mesh helpers and the real Sky Block;
  - `dandelion_kit.py` for the toothed leaves, the bud and the yellow bloom, and for the seed clock in 08;
  - the painted `DandelionGreens.png` atlas, for the leaves of the young dandelions.

  Setting `ONLY = ('03', '07')` before the exec builds a subset.
- **Shared base:** every concept stands on the same painted meadow. It has the production grass in three sizes, three young dandelions, white and aqua pebbles, the pebble path, the pearl dish tray with the real Sky Block, and three green status lights. The input is always two puffs of parachute seeds arriving from the player's front-left. Glass that holds a cloud uses a clear shell (`Concept_OrbShell`) instead of refracting glass, so the cloud inside can be seen.
- **Render and save:** run headless with Blender 5.3 (installed at `E:\Steam\steamapps\common\Blender\blender.exe`). Exec the build script, set these globals, then exec `render_sky_processor_concepts.py`:

  ```
  SCENE_NAME = 'Aero_SkyProcessorSilkConcepts'
  TOP_NAME = 'SilkProcessorConcepts'
  PREFIX = 'SilkProcessor'
  SHOT_DIR = '/_Screenshots-and-previews-to-delete/SkyProcessorConcepts/Round03'
  BLEND_PATH = '/ArtSource/Resources/SkyProcessorConcepts/Round03_SilkProcessors.blend'
  RES = (800, 1000)
  SAMPLES = 64
  GLARE = .1
  EXPOSURE = -.25
  ```

  Always set `RES`: the concept kit defines `RES` as a folder path, and the render script reads it as the still size. The run takes about 30 s.
- **Sheet:** `assemble_sky_processor_concepts.py silk03` writes `_Screenshots-and-previews-to-delete/SkyProcessorConcepts/Round03/SilkProcessor_Concepts_Round03.png`. `Lineup.png`, in the same folder, shows all ten side by side at the same scale.

**What the renders show:**
- **Heights:** 09 Silk Top is the lowest, at about 1.4 m in concept scale. 10 Kite Winch is the tallest, at about 3.3 m to the kite's nose, and 01 Silk Turbine reaches about 3.25 m. In production both of these would be checked against the water press.
- **The working moment:** each still shows the machine mid-cycle: seeds arriving, silk on its way to becoming cloud, and the finished block on the tray.

### Round 03 outcome

On 2026-11-15 the user chose **01 Silk Turbine**, without the green base and with more hand-painted machine details. It is now the Wind Condenser in the game; see [Wind Condenser: Silk Turbine](SKY_WORKSHOP.md#wind-condenser-silk-turbine-windcondenserprefab).

### Sources

- [A separated vortex ring underlies the flight of the dandelion (Cummins et al., *Nature* 562, 414–418, 2018)](https://doi.org/10.1038/s41586-018-0604-2)
