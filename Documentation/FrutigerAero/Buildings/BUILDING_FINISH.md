# Pearl marble, aquatic accents and bounded reflections

Implemented on 2026-10-25 from the balanced aquarium direction and existing-kit scope. Building samples 2, 3, 6, 9 and 11 inform the polished floors, aqua depth, restrained iridescence and luminous recesses. This is an implementation review, pending visual acceptance; existing accepted geometry and lighting decisions remain the baseline.

## Appearance and settings

`Assets/_Project/Environment/Lighting/BuildingFinish.asset` controls the finish. The four opaque building presets, their GUIDs, geometry, colliders, construction stages and gameplay data are retained. The transparent river's optical controls remain in its existing profile.

| Control | Default | Purpose |
| --- | ---: | --- |
| Reflections | 0.7 | Shared day/night environment and live deck reflections |
| Polish | 0.65 | Small increase to existing dielectric marble smoothness |
| Roughness variation | 0.025 | Broad world-space variation, continuous across tile joins |
| Inlay depth | 0.55 | Increased apparent separation of existing flowing inlay layers |
| Iridescence | 0.075 | Restrained cyan/lilac grazing-angle rim on aquatic areas |
| Glints | 0.8 | Filtered directional highlights on glazing |
| Edge light | 0.14 | Steady white-cyan accent within baked recessed aqua boundaries |

The ordinary floor and marble around river apertures use the same finish helper. White construction remains opaque; no see-through borders, green building colors, new particles or additional lights were introduced. The accepted river projection profile and day/night asset are unchanged.

## Reflection implementation

`AeroBuildingFinish` owns one hidden camera and one reusable HDR texture. It submits a URP single-camera request outside the main rendering loop, before the main camera renders. The dedicated renderer includes opaque geometry and characters, with transparency, post-processing, shadow-map updates and all recursive renderer features excluded. The mirrored view uses an oblique near plane and restores culling state in a finally block.

`TowerBuildingFinishBridge` supplies actual visible, completed deck identities. Selection searches below the camera, rejects hidden/unfinished tiles, and prunes distant chunks. A floor change invalidates the previous view and starts a 0.35-second blend through the environment fallback. Only matching horizontal deck surfaces receive the live image. Steps, vertical faces and other floors retain environment reflections.

The far renderer submits existing cached meshes and matrices for the selected floor and its ceiling, independently of the main view's frustum. Near renderers and the skinned character render through the reflection camera normally. Capture range is 32 m; receiver contribution fades over 24–32 m. Building fragments are clipped to the selected vertical volume and range during capture. No reflection meshes are generated or expanded from the tower.

Balanced PC uses half-resolution reflection output, capped at 1024 pixels on the longest edge: 960 × 540 at 1080p. Environment-only quality, mobile platforms, other pipeline assets, stereo cameras and unsupported requests fall back without a live view. Texture bindings are cleared before releasing or resizing the target. Disable/re-enable retries a failed capture.

Day and night use the existing aquatic cubemap with separate tint presets by default; artists can supply distinct cubemaps. Shared textures require one environment lookup rather than two. Settings and live textures are selected per camera/scene to avoid leaking one view into another.

## Review and validation

The manually invoked `AeroBuildingFinishReview` builds a disposable copy of all 86,467 cached tower assets at completed stages, preserving the original save and network state. Review images in [Finish](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Buildings/Finish/) show matched finish-disabled and enabled views; they are comparisons of the new effect, not historical captures. The actual FrutigirGirl visual is sampled in Edit Mode, without entering Play Mode.

The [camera-turn follow-up](../Lighting/CAMERA_TURN_PARTICLES_AND_FLARE.md) caches deck/instance bounds, batches reflection draws across chunks, and records the turning comparison. It also updates the separate day/night profile for the requested particles and sun flare. The original 2 ms estimate is a planning reference, not a feature restriction.

- Floor 0: interior, central glass, stairs, glass underside and a close arch view in daylight, sunset and night; walking/turning and character translation in [Motion.gif](../../../_Screenshots-and-previews-to-delete/Documentation/FrutigerAero/Buildings/Finish/Motion.gif).
- Floors 12 and 24: corresponding interior, glass, stairs and underside captures. Reports list the captured poses and bounded instance submissions.
- [Lifecycle inspection](Finish/Lifecycle.json): initial fade, floor 0 to 12 transition, environment fallback, live restoration, unfinished-tile rejection and resource release. Reflection submissions were 127 and 139 in the two inspected floor views. The target's reported graphics allocation was approximately 9.2 MiB after warm-up; driver counters can lag after reallocation.
- C# compilation and shader diagnostics were inspected directly. Automated tests and Play Mode were not used, following the explicit implementation request.

`Capture(floor, true)` stores intermediate motion PNGs in `Temp/AeroBuildingFinishMotion`; the deliverable animation is retained in this documentation folder. `StartBenchmark()` performs a manually started, asynchronous CPU review with three alternating baseline/environment/live blocks. `StopBenchmark()` closes the disposable scene and restores editor profiling state if instrumentation was enabled.

## Performance limitation and editor recovery

[Earlier timing snapshot](Finish/Performance-PreFinal.json) measured a 1.75 ms combined GPU-envelope difference at 1920 × 1080 on the RTX 4060 / Direct3D 12. Its environment-only result was slower than the live result, demonstrating substantial measurement noise. It is not evidence of a guaranteed speedup or a confirmed 2 ms budget.

A later attempt to isolate camera GPU scopes encountered a native driver crash: `d3d12: failed to wait for fence (258)`, followed by NVIDIA `OpenAdapter10`, Unity `D3D12DeviceState::Transition` and `D3D12CommandListWithState::ApplyStateClear`. The editor was reopened in this same project. No report was submitted to Unity. The cause of the driver failure has not been established.

The review helper therefore disables GPU instrumentation on this D3D12 workstation. [Final report](Finish/Performance.json) distinguishes CPU timings from unavailable GPU counters. **The 2 ms GPU target and live networked gameplay performance remain unconfirmed.** Editor rendering costs must not be presented as gameplay FPS.
