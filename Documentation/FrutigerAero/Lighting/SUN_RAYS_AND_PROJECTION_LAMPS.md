# Sun rays, glowing pieces and aqua projection lamps

The 2026-10-23 references add three related ideas: [sunlight shafts through trees](../../../Frutiger%20Reference/Asset/Sun-Beams-Forest-User.png), [luminous aquarium glass](../../../Frutiger%20Reference/Asset/Aquarium-Glow-User.png), and an [aqua water-projection lamp](../../../Frutiger%20Reference/Asset/Water-Projection-Lamp-User.png). Use warm neutral sunlight and white/blue aquatic fixtures while retaining the green landscape.

## Sun shafts

`AeroSunShaftRendererFeature` integrates light through the main directional shadow map. The saved sun is at ten degrees elevation, azimuth 330 and intensity 2.4 with a restrained warm tint. Side arches admit beams beneath solid covered floors; roof openings are not required. Pillars and side piers shape the light. Rays stop at opaque depth and are capped at 42 metres. Side-on scattering remains visible, while forward scattering is stronger. Sky contribution is reduced to preserve the blue sky outside the openings.

The effect runs at half resolution with 24 shadow samples (inspector range 8–24). A depth-aware nine-tap composite smooths the dither and avoids spilling light across foreground silhouettes. It adds one half-resolution RGBA16F render target and two passes, before post-processing. It reuses existing depth and main-shadow textures; no extra camera or shadow map is created. Zero intensity skips the effect; absent sun/depth/shadow resources skip recording. Reflection/preview camera types and overlay cameras are excluded, as in the existing nearby-light feature.

Settings are on **Aero shadow-shaped sun rays** in `Assets/Settings/PC_Renderer.asset`. The main directional light must cast shadows. Detail follows the current shadow map's resolution and distance; this is bounded shadow-map scattering rather than a world-sized fog simulation.

## Glowing pieces and bloom

The existing global atmosphere now uses bloom intensity **0.42**, threshold **1.05**, scatter **0.65**, clamp **12**, half-resolution filtering and four iterations. HDR and camera post-processing are enabled. Tonemapping and the existing grading remain in place.

The saved scene uses a soft trilight ambient fill so covered white stone stays readable. The faulty screen-space ambient-occlusion pass is disabled: its depth reconstruction added dark coplanar tile lines visible through translucent UI. Baked material occlusion and native directional shadows remain. Plain stone no longer emits its own moving caustic texture; aquatic glass inlays retain their internal glow.

Aquatic architectural inlays have a separate `_GlazeGlow` multiplier (the four live kit materials use 1.8). Incoming construction edges emit brighter HDR cyan during their reveal. Completed white stone keeps its existing opaque finish. The new lamp's frosted core and lens use HDR emission, a larger water-light pattern and a gentle brightness pulse. Emission flags are configured explicitly so Unity's material validation retains the glow keyword. Bloom is applied before overlay UI.

## Artificial water projections

`Assets/_Project/Prefabs/Lighting/AquaProjectionLamp.prefab` is a reusable pearl/azure lamp with a frosted aqua cube, rounded corner posts, lens and canopy. It contains a real URP spotlight, so its cookie projects onto normal Lit materials and the building kit. The architectural forward/glass passes now compile light-cookie support.

The spotlight's 128×128 animated cookie combines two flowing caustic layers with a soft aperture. It updates at **8 Hz**, reuses the same render texture/material, and increments the texture's update counter so URP refreshes its cookie atlas. Its range is 10 metres. Nearby lamps are selected every quarter second: at most **four within 42 metres** have active lights/cookies, and only the nearest casts shadows at the lowest configured additional-light shadow tier. Distant/disabled lamps release their cookie render targets and materials. No projection cameras, receiver scripts, particles or added gameplay colliders are used.

Two prefab instances sit beside the starting workshop, grounded against the actual terrain. They are exterior workshop fixtures, separate from locked construction floors. Runtime lighting remains local visual presentation; it does not change resources or construction authority.

`AeroAtmosphereAuthoring.Apply()` installs/configures the renderer feature, bloom, kit glow and lamp assets through Unity APIs. Scene-save hooks temporarily clear generated cookie references during serialization and restore them afterward, keeping scene files free of transient texture references.

## Review and validation

`AeroAtmospherePreview.Capture()` creates a disposable Edit Mode bay with a solid roof, open sides, the actual building kit and a projection lamp. It captures shafts/bloom on and off; `Capture(true)` samples projector motion. `TowerLightingRepairPreview.Capture()` additionally reviews 1,500 pieces from the saved tower cache, with the completed overhead floor, actual sun settings and HUD. It changes no construction stages. Review scenes, camera targets, clocks, bloom and lamp budgets are restored afterward.

The EditMode checks cover actual scattering pixels, an invisible shadow-only occluder, zero intensity/missing sun, real projection onto stock Lit surfaces, animation reaching the URP cookie atlas, HDR/bloom references, nearest-four/one-shadow budgets and texture cleanup. PlayMode checks verify moving shadow occlusion, throttled cookie updates/reuse/release, and the existing nearby light/tower progression behavior. Play Mode is entered only through the final automated runner.

Render Graph review: depth and main-shadow reads are explicit, transient handles are assigned on every recording, pass callbacks are static, and auxiliary textures are bound during execution without publishing globals. The half-resolution descriptor is derived from the active color descriptor, intentionally changing size, format, depth and MSAA. The final color attachment is read/write for additive blending. Both passes perform custom shadow integration or depth-aware filtering, so native raster passes are intentional. The current project compiles and renders these paths; representative player-build profiling is still required for a numerical FPS claim.

`SunRaysValidation.json` preserves the earlier implementation's verification. The current repair adds covered-roof/side-shutter, water-source exclusion, nonnegative edge, and interface-pixel regressions; see `LightingRepairValidation.json`. The Editor also experienced a Direct3D 12 device crash during diagnosis and was reopened with the session-only `-force-d3d11` option. Build graphics settings were not changed.
