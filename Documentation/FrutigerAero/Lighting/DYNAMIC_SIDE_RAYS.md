# Dynamic side-entry tower lighting

Accepted by the user on **2026-10-24** as the current tower interior lighting baseline. **Calmer interior (2026-10-31):** the user found the interior too bright and the volume too dramatic. Changes: shafts 1.65 → 1.15, fills 2.2 → 1.5, density 0.035 → 0.028, noise contrast 0.65 → 0.45, haze 0.12 → 0.05, and fill scattering capped at its 3 m value (`AERO_FILL_SCATTER_CAP`). `TowerExitAtmosphere` became a calm interior grade: exposure −0.4 instead of +0.13, no extra contrast, bloom 0.35, shafts ×0.9. Its weight now covers the whole interior (65% deep inside, 100% at the open edge, × 0.8) instead of only the edge. The accepted result is shown in the [lighting capture](DynamicRays/Floor0-Side3-After.png), [standing motion](DynamicRays/Motion.gif) and [walking preview](DynamicRays/Walk.gif). Future lighting changes should preserve this presentation and the construction-aware behavior described below; the measured fixture budget and validation limits remain recorded in this document.

The tower uses sun-shadow scattering inside its viewed floor volume. Pillars split the shafts, completed decks block light between floors, and the existing day/night cycle supplies the sun/moon direction and color. The fixed crossed particle sheets have been removed from Tower. Fireflies remain a separate system.

## Settings

Select `Assets/_Project/Environment/Lighting/TowerInteriorRays.asset` to adjust quality, density, intensity, edge softness, world-space noise scale/velocity/contrast, interior haze, fill strength/range/angle/pitch, source hysteresis and transition duration. The saved default is Balanced PC. `AeroDayNight.asset` remains the time and sunlight authority.

| Preset | Resolution | Integration | Maximum distance | Fill sources |
| --- | --- | --- | --- | --- |
| Balanced PC | Half | 16 steps | 32 m | 2 |
| Low | Quarter | 12 steps | 32 m | 1 |

Native shadow distance remains 50 m. Fill sources request the existing medium shadow tier: 512 px, inside the shared 2048 px additional-light atlas. Other scene lights can still cause URP to reduce atlas allocations under pressure; 512 px is a request, not a separate reserved atlas.

## Architecture and transitions

`TowerAtmosphereBridge` belongs to Gameplay and supplies Rendering with actual built arch sockets and floor visibility. Rendering has no Gameplay assembly reference. The bridge caches shadow meshes per chunk/version for the viewed floor and its ceiling, briefly retaining the outgoing volume during a floor transition. It does not enable shadows throughout the tower. Large debug teleports retain only endpoint floor/ceiling pairs.

Near assets cast their actual construction-stage geometry and receive shadows. Distant completed slabs and pillars use simplified combined shadow-only meshes; arches retain their open passage. Partial stages use their actual render mesh. Blueprint stage 0, hidden floors, completed walking colliders for unfinished pieces, and pooled inactive assets do not become solid occluders. Retired proxies deactivate immediately before deferred runtime destruction, so rollback cannot leave an extra shadow for a frame. Near chunk changes activate/deactivate existing proxies rather than rebuilding their meshes. Construction-stage and floor visibility events invalidate affected versions.

Two reusable spotlights choose real arch openings at 10 Hz. Source identity uses each cached chunk's fixed asset offset plus local asset index, avoiding hash collisions; a full-tower regression verifies uniqueness and stability through construction changes. Hysteresis prevents small movements from changing source selection. A source fades to zero before moving to another fixed socket, then fades in. Spotlights use the same native URP light data, cone attenuation and shadow atlas in both surface lighting and scattering. Direct sunlight remains stronger; nighttime fill is restrained blue.

The ray marcher stops at opaque depth and clips to cylindrical interior volumes. Adjacent/outgoing floor volumes blend rather than dragging haze with the camera. Advected 3D value noise moves in world space. Density rises near openings, stays subtle in the center, fades at floor boundaries and rejects exterior space. Exterior pixels without an interior intersection skip integration entirely. Depth-aware compositing occurs before post-processing and screen UI.

Water caustics, projection lamp cookies, ocean geometry, construction rules, interface behavior and the separate firefly material/system are retained.

## Review material

Captures in [DynamicRays](DynamicRays/) use disposable completed floor/ceiling copies from the actual tower data, with no changes to saved progression. `Before` disables scattering and the two fill sources; architecture shadows remain enabled to isolate the atmosphere. These are effect-disabled comparisons, not historical screenshots of the old beam sheets.

- Floors 0, 12 and 24: all four sides, center, stairs visible along the perimeter, sunrise, noon, sunset and night.
- [Matched before](DynamicRays/Floor0-Side3-Before.png) and [after](DynamicRays/Floor0-Side3-After.png).
- [Standing motion](DynamicRays/Motion.gif): 36 frames, 4.32 seconds, fixed camera.
- [Walking/rotation preview](DynamicRays/Walk.gif): 36 frames, 4.32 seconds, world-space density and fixed source positions.
- [Capture metrics](DynamicRays/CaptureMetrics.json) record image changes; these are comparison evidence, not a substitute for visual review.

`TowerRayReview.Capture(floor)` recreates the review in Edit Mode, restores scene lighting and closes its disposable scene. `TowerRayReview.SaveBenchmarkFixture()` exports the reusable review snapshot and source sockets. The benchmark reuses source meshes and production radial instancing rather than duplicating vertex buffers.

## Validation and limits

Edit Mode render comparisons cover sunlight occluders, a covered side opening with a shutter, opaque roof blocking at noon, world-noise repeatability after camera rotation, native fill-shadow agreement, exterior rejection and finite positive pixels. Additional checks cover quality presets, ordinary camera context selection, stair-volume blending, fade-before-relocation, independent fireflies, partial construction, blueprint rejection, hidden floors, rollback and cache reuse. Existing water, projection-lamp, ocean, day/night, progression and resource checks were also run.

Final Play Mode verification runs only through Unity TestRunner after implementation is complete. The GPU comparison uses an automated 1920 × 1080 completed floor/ceiling fixture and the production bounded shadow cache, three alternating blocks and 180 recorded frames per mode after transitions warm up. Baseline disables the effect, fills and bounded casters; enabled modes include them. The [performance report](DynamicRays/Performance.json) distinguishes GPU timestamps from CPU frame time and records unavailable counters explicitly. Editor overhead and a controlled completed-floor fixture limit extrapolation to the full networked game.

See [validation results](DynamicRays/Validation.json) for final counts and the unrelated UI/font/interaction failures. No claim that the complete suite passes should be inferred from lighting-specific passes.

Final targeted results: **66/66 Edit Mode** and **15/15 Play Mode**. The broader diagnostic snapshots recorded three Edit Mode and seven Play Mode failures outside the lighting scope; their names and messages remain in the report.

Measured on the current **RTX 4060 / Direct3D 11**, using GPU-collecting camera-envelope timestamps at 1920 × 1080:

| Mode | Median camera GPU | Added versus disabled |
| --- | --- | --- |
| Disabled | 1.85 ms | — |
| Balanced PC | 3.54 ms | **1.69 ms** |
| Low | 2.76 ms | **0.90 ms** |

This meets the approximately 2 ms goal in the controlled completed-floor fixture, including its bounded shadow cache and fill lighting. It is not a guarantee for every full-game view. The final Balanced scattering/composite subtotal is about 1.03 ms; skipping native fill-shadow lookups outside the spot cones reduced the earlier approximately 2.18 ms subtotal. GPU durations come from `Recorder.gpuElapsedNanoseconds` with GPU profiling explicitly enabled, not CPU marker duration or CPU frame time. Whole-Editor GPU counters were unavailable; a test-only GPU command envelope around the review camera supplies the complete-camera comparison.

## Technical references

The implementation follows the installed URP package's `ForwardLights` visible-light packing and `Shadows.hlsl` sampling contracts. Explicit caster availability handles URP's stripped shadow-off variants and empty shadow atlas without reusing stale cascade coverage. The resource review is in [Render Graph validation](DYNAMIC_RAYS_RENDER_GRAPH_REVIEW.md). Unity's [URP lighting manual](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lighting-landing.html) and [Bloom reference](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-bloom.html) provide the pipeline and post-processing context.
