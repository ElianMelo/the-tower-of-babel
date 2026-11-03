# Tower lava lamps

Lava lamp sconces hang like torches on the **inside** of the tower's pillars, over the deck and the winding stair ramp: **one lamp, then three bare pillars**, around every ring. (They first hung on the facade; moved inside on 2026-11-02.) Since 2026-11-03 lamps are also scattered on the **outside** of free pillars, following two rules: a pillar carries **one lamp at most** (inside or outside, never both), and **two lamps never hang on neighbouring pillars** (at least one bare pillar between any two). References: [lava lamp set](../../../Frutiger%20Reference/INDEX.md#tower-lava-lamps--2026-11-02).

## Model

`Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab` is the lava lamp version of the [aqua projection lamp](SUN_RAYS_AND_PROJECTION_LAMPS.md). `LavaLampAuthoring.BuildPrefab()` builds it procedurally (menu **Tower of Babel/Environment/Apply tower lava lamps**):

- **Chrome sconce** (one mesh, `LavaLamp_Sconce.asset`): a stepped wall rosette, a torch arm that rises gently out 0.52 m with a brace and finial, a hook and hanging ring, three interlocked oval-link chains to a cradle collar, the hourglass base and the conical cap. Material `LavaLampChrome.mat` (URP Lit, metallic 0.92, smoothness 0.84).
- **Lava glass** (`LavaLamp_Glass.asset`): the tapered vessel, 0.455 m tall, radius 0.095 m to 0.055 m.
- **Water projection spotlight**: the same animated caustics cookie material as the aqua lamp (`AquaProjectionCookie.mat`, see [Caustics cookie](#caustics-cookie)). On the tower the lamps use their own light budget, not the four-lamp `AeroProjectionLamp` budget. It sits just over the cap and points up, leaning 18° toward the wall so the pattern washes the arch and pillar above. Range 9 m, 110° cone.
- **Lava glow**: a small shadowless blue point light (2.6 m) in the glass that lights the lamp's chrome, its chains and the pillar behind.

The sconce origin is where the wall plate meets the pillar. The lamp hangs 1.15 m below the arm; no colliders. Inside, the spotlight washes the arch spandrels above the pillar and the underside of the glass ramp.

## Fluid shader

`TowerOfBabel/Lighting/Lava Lamp Fluid` (`Shaders/AeroLavaLamp.shader`) ray marches the wax inside the glass mesh in object space, based on the common lava-lamp technique of signed-distance metaballs:

- Six wax ellipsoids plus a bottom pool and a thin top pool, joined with a polynomial smooth minimum (merge radius 0.034).
- Each blob runs a heat cycle of 15–28 s: cosine travel lingers at the bulb and under the cap; moving blobs stretch into teardrops. Every lamp gets different wax from a hash of where it hangs, so instanced lamps never move in step.
- The field is intersected with the inner glass wall, so wax presses flat on the glass and never leaves the vessel.
- Shading: wax lit from the bulb below with a wrapped term, deeper core, glowing translucent rim and a soft sheen; blue fluid absorbs with path length and is brighter low down; soft green scatter around blobs; a bright ring at the base.
- The glass shell adds Fresnel reflection from URP probes, a sun glint, vertical highlight streaks and a clear tinted edge where the ray crosses only glass.
- Up to 34 march steps near, 12 beyond 45 m. Opaque, instanced, with a depth pass.

`_AeroDaylight` (set by the day/night cycle) scales the glow: **4.6 at night, 1.3 by day**, so lamps stay visible in daylight and pass the bloom threshold (1.05) at night. `_Daylight` and `_FlowTime` override it for previews and tests.

## Caustics cookie

Since 2026-11-03 the projection is no longer one scrolling 128 px still refreshed 8 times a second. `Hidden/TowerOfBabel/Water Projection Cookie` samples `Environment/Lighting/Caustics/LavaLampCaustics.png`, a 32-frame, 512 px looping caustics animation baked for this project by `Tools/Caustics/bake_caustics.py` (light rays refracted through a tileable wave surface and gathered where they land, so the filaments are physically focused, not painted). Imported as a BC4 Texture2DArray with mipmaps.

- Neighbouring frames blend with a smoothstep; the loop lasts 9 s.
- Two layers at unrelated scales, angles and frame phases are combined with `min` plus a little of their sum, after [Ameye's realtime caustics notes](https://ameye.dev/notes/realtime-caustics/): sharp crossings, softly filled cells.
- Red and blue are sampled at slightly different scales (1.8 %), giving the coloured fringes real water light has.
- Soft round aperture; a faint 4 % floor keeps the cone readable between filaments.
- On the tower all lit lamps share one 512 px cookie rendered 30 times a second; each lamp turns it by its own angle (hashed from its pillar), so neighbouring washes never repeat. The aqua workshop lamps keep their own cookies, now 256 px at 24 Hz.

Free downloads were reviewed first (OpenGameArt CC0 sets by Calinou, mads and leeor_net). They are 256 px or soft blurred blobs, so the lamp uses the baked set; the finest download, leeor_net's 16-frame loop, now drives the [nearby water light](AQUATIC_LIGHT.md). Attribution: `Environment/Lighting/Caustics/THIRD_PARTY_NOTICE.md`.

## Placement and runtime

`TowerLavaLamps` (scene object **Lighting & Atmosphere / Tower lava lamps**) reads pillars from the chunk cache, sorts each ring by angle and takes every fourth. A ring with a bay count that is not a multiple of four closes with one shorter gap, unless that would put the last lamp next to the first; then that lamp is left out. Next, every pillar that is bare and has bare neighbours gets an outside lamp with chance `exteriorChance` (0.5), from a stable hash of floor, pillar and `placementSeed`, so the scatter never changes between sessions. Outside lamps face away from the tower at the same height. The saved tower has **1,488 inside and 1,493 outside lamps** (2,981 in all). The sconce sits at 3.6 m on the inner face of the pillar shaft (radius read from the pillar mesh, 0.381 m), just under the capital band, so the lamp's base is about 2.25 m above the deck. `interior` switches back to the facade.

The stair ramp is 4 m wide, runs against the inside of the pillar ring all the way round and climbs a full floor per ring, so it passes every interior lamp at some height. A lamp that would touch the steps is left out and the pattern carries on: **1,499 of 2,067** pattern slots keep a lamp. Where the ramp runs just below a lamp, someone climbing along the pillar edge can pass through it (lamps have no colliders). This was the chosen trade-off: `stairHeadroom` (default 0) also keeps a climber's head clear — 1 m leaves 1,165 lamps, 2.05 m leaves 823 with long bare stretches.

- A lamp shows once **its pillar is complete**, on floors the player can see; stage changes and floor unlocks refresh only the affected floor.
- Lamps draw with `Graphics.RenderMeshInstanced` per floor, culled by frustum and a 260 m distance; the tower carries no lamp GameObjects.
- Up to **24 light rigs** (prefab instances with their renderers off and their `AeroProjectionLamp` removed) move every 0.25 s to the nearest built lamps **in view** within 90 m. A lamp counts as in view when the camera frustum sees the wall its spotlight washes (about 6 m above it), so lamps just below the screen edge still light the arches in shot. Rigs that stay on a chosen lamp are not moved. The renderer is Forward+, which has no per-object light limit.
- Only the nearest lamp within 20 m casts spotlight shadows.
- Lights fade out over the last quarter of their reach (67–90 m), so lamps never pop on or off.
- Rig lights are full strength at night and 30% by day.

Runtime lighting is local presentation; it changes no construction state.

## Review and validation

`LavaLampPreview.Capture()` rebuilds a real section of the cached tower (floor 4), stairs included, around one lamp in a disposable additive scene with the Tower sky and reflections. All lamps within 26 m are lit with the shared cookie, as in the game. It writes sconce, fluid, arcade, projection (two moments) and other-face shots for day and night to `_Screenshots-and-previews-to-delete/LavaLamps/`.

- EditMode `TowerLavaLampEditModeTests`: outside lamps only on free pillars and facing out, never neighbours or two per pillar, stable random scatter, the ring-seam rule, every lamp in view lit (more than four) with one shared 512 px cookie turned per lamp, distance fade; and the earlier checks: one-in-four pattern and inward mount transform, pillar-built visibility and rollback, hidden upper floors, stair rejection keeping the pattern, the headroom option, nearest-light selection and release, day dimming, prefab wiring (cookie reuse, upward spot, instancing, mesh/shader dimensions), and the Tower scene placement.
- EditMode `LavaLampShaderEditModeTests`: blue fluid with green wax, wax motion over time, night glow much brighter than day and over the bloom threshold.
- EditMode `AeroCausticsFlipbookEditModeTests`: flipbook imports, cookie sharpness, colour split, motion and aperture.
- PlayMode `TowerLavaLampPlayModeTests` (automated runner only): instanced drawing, live upward projections with the shared cookie animating many times a second, day dimming and release at distance.
