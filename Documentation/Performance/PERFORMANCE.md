# Performance

There is no frame-rate target. The game renders uncapped and the goal is the highest frame rate the hardware delivers
at the chosen graphics quality. Every change is judged by the frame time it removes, measured with the benchmark below.
(`GameSettingsApplier` clears the 500 fps target the networking library sets when a session starts.)

## Far tower: GPU culling (2026-11-12)

Measured before this pass, from outside the tower (160 m, whole tower in view): 16.3 ms per frame in the Editor, of which
about 9 ms of GPU time was tower geometry and about 8 ms of render-thread time was per-draw state for ~300 draws.

Why it was slow:

- **Pages, not instances, were culled.** Far pieces were grouped in pages of up to 511. A page drew all of its instances
  when any corner of it was in view and in range. A lava lamp is 13,108 triangles and a vine about 4,000: three lamp
  pages near the 140 m detail distance alone submitted ~20 million triangles, most of them out of range or out of
  sight. In total the view submitted about 37 million triangles.
- **Nothing hidden behind the decks was skipped.** From outside, most of each floor lies behind the floor below it;
  from inside, the ceiling hides every floor above.
- **One draw per page run.** About 300 non-batched draws, each with its own property block.

What draws the far tower now (`InstancedFarChunkRenderer.Culling.cs`, `Resources/Rendering/AeroFarCulling.compute`):

- Every far instance lives in one set of GPU buffers (transforms, inverse transforms, bounding box). A chunk writes its
  span when it loads, changes or leaves; nothing per instance is copied or tested on the CPU each frame.
- Just before the camera renders, **one compute pass tests every instance**: view frustum, draw distance, deck occlusion
  and mesh level (LOD), each from the instance's own box and distance. Visible instances are appended to the list of
  their mesh level.
- **Each mesh level of each piece is one indirect draw** (about 30 draws for the finished tower instead of ~300).
- The lava lamp chrome (a plain URP Lit material) draws through `TowerOfBabel/Rendering/Far Lit`: the same Lit passes
  with the procedural instancing the other tower shaders already had.
- River glass composition draws one indirect command per floor from the same visible lists instead of re-sending every
  page's matrices each frame.

**Glass draws in a fixed order (2026-11-14).** The compute pass appends visible instances in whatever order its
threads finish. That is fine for opaque pieces (the depth buffer sorts them) but the 21,120 glass stairs blend, so two
steps that overlap on screen show the order they were drawn in: the order changed every frame and distant stairs
flickered. Batches whose material blends (render queue above 2500) are now "ordered": the cull only marks each
instance with its mesh level, a second kernel counts the visible instances of every chunk, and a third writes them
chunk by chunk (floor, then x, then z: the order the page path drew them in). The list is the same every frame for
the same view, whatever order the chunks loaded in. No measurable cost.

The page paths (resident runs and `RenderMeshInstanced`) remain as the fallback: without compute shaders, with baked
light probes in the scene, and for a shader without the procedural variant. `InstancedFarChunkRenderer.GpuCulling`
switches them for tests and comparisons.

### Deck occlusion

Occlusion is exact for the camera position. It uses no screen-space depth and no data from an earlier frame, so nothing
pops in late when the camera moves.

- `FarOccluderSet` keeps, for every floor, a top-down map (384 x 384 texels) of where the deck is **solid and opaque**.
  `ChunkManager` feeds it the completed floor tiles of every visible floor, near chunks included.
- A texel is solid only when all of it is opaque deck. Texels near any stretch of tile edge that borders a hole
  (stairs, an unbuilt tile, the deck rim) stay open. River tiles are solid where their marble is and open over the
  water channel, carved with the same distance function the glass floor shader clips with. Glass steps never occlude.
- A tile that has just reached its final stage is still assembling on screen: it occludes two seconds later. A tile
  that is rolled back, or a floor that is hidden, stops occluding in the same frame.
- In the compute pass an instance is hidden when it lies entirely beyond a deck plane (seen from the camera) and the
  projection of its box onto that plane, from the camera, falls on solid texels only (one summed-area-table lookup per
  deck). A deck closer to the camera than four near-plane distances is ignored, because the near plane may cut it.

A first version rasterized the decks into a depth pyramid for the camera. It was dropped: a pillar standing on a deck is
at the same depth as the deck under it, so a depth comparison almost never rejects it.

### What is unchanged on High

High shows the same picture as before this pass. Culling only removes what cannot be seen; mesh levels switch at the
same authored distances (70 m and 140 m, now per instance instead of per page); vines keep their full mesh.

Vines and lava lamps are the expensive detail (about 4,000 and 13,000 triangles each). Before, a page of them drew
whole when its nearest corner was within 140 m, so detail reached 140 m plus the size of the page, and from further
away none of it drew. Now each instance is tested on its own: **170 m on High**, about the reach the pages had. A first
version used 260 m; it drew every vine and lamp of the tower from the distant views and made them slower than before
(17.2 ms instead of 13.3 ms at 260 m), so it was brought back. The distance is a slider in the Graphics tab (up to
400 m) for anyone who prefers detail over frame rate.

## Other changes in this pass

- `MouseRotator` read the mouse in `FixedUpdate`: on frames without a physics step the movement was dropped, so the
  look speed fell as the frame rate rose. It now runs once per rendered frame.
- `PlayerInteractionRaycaster` looked up the interactable under the crosshair every frame with an allocating query; it
  now does so only when the target changes.
- A chunk is routed only to the far batches it has instances in (there are about two hundred batches, a chunk uses a
  handful).
- Lava lamp lights (2026-11-14): 96 instead of 24, chosen among the lamps the player can see, fading instead of
  switching. This is the one change of the pass that costs frame time for a better picture; see
  [lava lamps](../FrutigerAero/Lighting/LAVA_LAMPS.md).

## Graphics quality

Low, Medium and High in the settings menu (Escape, Graphics tab) set every value below; each can also be changed on its
own. See [the settings menu](../SETTINGS_MENU.md) for the full table.

- **High** (default): the scene as authored, nothing reduced.
- **Medium**: 2048 px shadows with two cascades to 80 m, grass and terrain detail pulled in, tower detail to 120 m, mesh
  LOD at 70% of the distance, quarter-resolution sun shafts, cheaper clouds, 40 lamp lights.
- **Low**: 1024 px single-cascade shadows to 40 m, 75% render scale, far plane at 1.5 km, sparse grass to 30 m, tower
  detail to 60 m, mesh LOD at 35% of the distance, no sun shafts, live floor reflections or full river refraction,
  12 lamp lights.

## What was already in place (2026-11-03)

- Mesh-only LOD with the same material: finished Floor, Stair, Pillar, Arch and GlassWaterFloor draw ~50% triangle
  meshes past 70 m and ~30% past 140 m (stairs stay at 50%). Build with **Tower of Babel/Performance/Build far mesh
  LODs** (`TowerMeshLodAuthoring`).
- Shadow budgets: sun 120 m / 3 cascades / 4096 (2048 made the interior god rays shimmer, 2026-11-10); spot-light atlas
  1024 with 256 px shadows; river cookie 1024, river shadows within 20 m.
- Nearby water light skipped when no lake is in view; river glass floors that refract nothing share one render pass.
- Rejected after measuring: depth priming (the prepass cost 10 ms) and cheap far shaders with vine image cards (they
  popped and made vines disappear).

## Measuring

`FrameRateBenchmarkPlayModeTests` (Editor Play Mode, 1920 x 1080, vsync off) plays the saved Tower as host with the whole
tower built and measures fixed views: frame time, main thread, render thread, GPU, and per-system markers. It writes
`Documentation/Performance/FrameRateBenchmark.json`. A built game runs the same benchmark with `-towerBenchmark`.
Editor numbers include the Editor's own Game view work; compare runs on the same machine.

### Before and after this pass (RTX 4060, Ryzen 5 5500, High quality)

Frame time is the median of 90 frames; before measured 2026-11-12, after 2026-11-14. "After" is the game as it is
now, with 96 lava lamp lights. The middle column is the same code with the 24 lights the scene had before, measured
in the same session: the difference is what the extra lights cost.

| View | Before | After, 24 lamp lights | After (96 lamp lights) | Frame rate |
| --- | --- | --- | --- | --- |
| Inside, floor 15, across the tower | 12.1 ms | 9.0 ms | 9.6 ms | 83 -> 104 fps |
| Inside, floor 15, looking outward | 5.8 ms | 5.2 ms | 5.6 ms | 173 -> 179 fps |
| Outside, ground, 160 m | 16.3 ms | 9.2 ms | 10.3 ms | 61 -> 97 fps |
| Outside, ground, 160 m, far tower hidden | 9.2 ms | 8.2 ms | 8.9 ms | 108 -> 113 fps |
| Outside, ground, 260 m | 13.3 ms | 8.0 ms | 7.9 ms | 75 -> 126 fps |
| Outside, air, 300 m | 12.8 ms | 7.9 ms | 8.4 ms | 78 -> 119 fps |

Where the time went, outside at 160 m with the whole tower in view:

| | Before | After (96 lamp lights) |
| --- | --- | --- |
| Main thread | 14.3 ms | 8.6 ms |
| Render thread | 8.6 ms | 3.9 ms |
| GPU | 13.2 ms | 9.6 ms |
| Far tower draws | 307 | 28 |

Runs on the same code differ by up to about 0.5 ms per view (three runs with 96 lights gave 9.5 to 9.8 ms inside
and 10.3 to 11.6 ms outside at 160 m). With the far tower hidden the 160 m view costs 8.9 ms, so the whole far tower
costs about 1.4 ms of frame time from outside; what is left is the base scene (shadows, terrain, grass, lamp lights)
and the Editor's own Game view work.

Not measured: the Medium and Low presets, and a built player (the Editor adds its own overhead to every number here).
