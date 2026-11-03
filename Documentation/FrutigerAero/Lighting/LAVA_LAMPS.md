# Tower lava lamps

Lava lamp sconces hang like torches on the **inside** of the tower's pillars, over the deck and the winding stair ramp: **one lamp, then three bare pillars**, around every ring. (They first hung on the facade; moved inside on 2026-11-02.) References: [lava lamp set](../../../Frutiger%20Reference/INDEX.md#tower-lava-lamps--2026-11-02).

## Model

`Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab` is the lava lamp version of the [aqua projection lamp](SUN_RAYS_AND_PROJECTION_LAMPS.md). `LavaLampAuthoring.BuildPrefab()` builds it procedurally (menu **Tower of Babel/Environment/Apply tower lava lamps**):

- **Chrome sconce** (one mesh, `LavaLamp_Sconce.asset`): a stepped wall rosette, a torch arm that rises gently out 0.52 m with a brace and finial, a hook and hanging ring, three interlocked oval-link chains to a cradle collar, the hourglass base and the conical cap. Material `LavaLampChrome.mat` (URP Lit, metallic 0.92, smoothness 0.84).
- **Lava glass** (`LavaLamp_Glass.asset`): the tapered vessel, 0.455 m tall, radius 0.095 m to 0.055 m.
- **Water projection spotlight**: the same animated water cookie material as the aqua lamp (`AquaProjectionCookie.mat`) and the same `AeroProjectionLamp` budget. It sits just over the cap and points up, leaning 18° toward the wall so the pattern washes the arch and pillar above. Range 9 m, 110° cone.
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

## Placement and runtime

`TowerLavaLamps` (scene object **Lighting & Atmosphere / Tower lava lamps**) reads pillars from the chunk cache, sorts each ring by angle and takes every fourth. A ring with a bay count that is not a multiple of four closes with one shorter gap. The sconce sits at 3.6 m on the inner face of the pillar shaft (radius read from the pillar mesh, 0.381 m), just under the capital band, so the lamp's base is about 2.25 m above the deck. `interior` switches back to the facade.

The stair ramp is 4 m wide, runs against the inside of the pillar ring all the way round and climbs a full floor per ring, so it passes every interior lamp at some height. A lamp that would touch the steps is left out and the pattern carries on: **1,499 of 2,067** pattern slots keep a lamp. Where the ramp runs just below a lamp, someone climbing along the pillar edge can pass through it (lamps have no colliders). This was the chosen trade-off: `stairHeadroom` (default 0) also keeps a climber's head clear — 1 m leaves 1,165 lamps, 2.05 m leaves 823 with long bare stretches.

- A lamp shows once **its pillar is complete**, on floors the player can see; stage changes and floor unlocks refresh only the affected floor.
- Lamps draw with `Graphics.RenderMeshInstanced` per floor, culled by frustum and a 260 m distance; the tower carries no lamp GameObjects.
- Four light rigs (prefab instances with their renderers off) move to the nearest built lamps within 42 m every 0.25 s. The existing `AeroProjectionLamp` budget then keeps at most four cookies active, shadows on the nearest only. Rigs that stay on a chosen lamp are not moved.
- Rig lights are full strength at night and 30% by day.

Runtime lighting is local presentation; it changes no construction state.

## Review and validation

`LavaLampPreview.Capture()` rebuilds a real section of the cached tower (floor 4), stairs included, around one lamp in a disposable additive scene with the Tower sky and reflections, and writes sconce, fluid, interior arcade and projection shots for day and night to `_Screenshots-and-previews-to-delete/LavaLamps/`.

- EditMode `TowerLavaLampEditModeTests`: one-in-four pattern and inward mount transform, pillar-built visibility and rollback, hidden upper floors, stair rejection keeping the pattern, the headroom option, nearest-light selection and release, day dimming, prefab wiring (cookie reuse, upward spot, instancing, mesh/shader dimensions), and the Tower scene placement.
- EditMode `LavaLampShaderEditModeTests`: blue fluid with green wax, wax motion over time, night glow much brighter than day and over the bloom threshold.
- PlayMode `TowerLavaLampPlayModeTests` (automated runner only): instanced drawing, live upward projections with the animated cookie on the nearest lamps, day dimming and release at distance.
