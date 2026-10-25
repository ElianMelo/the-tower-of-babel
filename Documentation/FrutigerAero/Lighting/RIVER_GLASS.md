# Water inside glass floor pools

The 2026-10-24 enhancement separates the moving aqua water from its smooth glass enclosure. It retains the two rings, cardinal spokes, central pool, 20 cm slab envelope, walking colliders, construction stages, costs and save identities. Existing edits to the Tower scene and day/night asset were preserved.

## Gameplay regression repair

The first enhancement expanded every submitted water batch into CPU-clipped water and glass meshes inside RecordRenderGraph. Completing the saved tower exposes 19,114 water tiles, and this unbounded synchronous work could stall the main thread and allocate large amounts of geometry. The previous fixture-only verification missed the real full-tower cheat and subsequent interaction.

The production renderer now submits the original shared tile meshes and instance matrices directly for the two surface passes. The shader performs channel masking, radial deformation and water inset. It never reads back vertices or creates per-batch meshes. RiverPoolGeometry's clipping builder remains an isolated authoring/testing helper; the renderer does not call it. Distant floor pages now cast native shadows only when they overlap an active local river projector; TowerAtmosphereBridge retains its bounded sun-occlusion cache.

Regression verification uses the actual saved Tower scene and networked cheat UI. It completes all 45 floors / 87,972 assets, renders the result, checks native memory growth below 256 MiB, and successfully issues Reset tower afterward. A separate case completes the first floor, targets an unlocked next-floor pillar through a real physics raycast, and completes construction through the normal interaction/server path.

## Appearance and geometry

The shell uses the original slab's top and bottom surfaces with separate smooth reflection and boundary shading. The water pass insets the shared geometry from 4 cm to 18 cm below the walking surface on the GPU, leaving a 2 cm cover, 2 cm clear gap and 2 cm base. Its channel mask is inset 2 cm from the banks. Internal tile side faces are suppressed. Thin polished channel rims and approximately 12-metre joints frame pool sections; the central pool receives sparse grid joints.

Both passes support GPU-bent radial source tiles and the existing CPU-bent near meshes, preserving original patch coordinates for construction reveal. Batches retain their floor identity and 511-instance limit. The surface feature owns no generated geometry cache. Pooled floor-group buffers are reused during collection.

The completed opaque material now shades marble directly, skipping the retired painted-water calculations. Apertures remain open in the opaque forward, depth and shadow passes. Partial construction retains its previous presentation.

Water owns the moving ripple normals, broad swells, bounded refraction, white internal caustic highlights and environment reflection. Glass owns the smooth reflection, sharp glints, edge tint and frame details. Its pass performs no scene-color copy or second refraction. Absorption depends on enclosed water thickness and view angle, not distance to a lower floor. Distortion rejects foreground depth discontinuities and fades at screen edges. Distant caustic sampling uses texture derivatives rather than forced mip zero.

Installed Stylized Water 3 SmoothWaves, Caustics_1 and Caustics_2 assets are referenced without modifying vendor files. Reflections use the existing environment and sun; there is no extra reflection camera or live character reflection.

## Softer water detail and surface controls

The [latest user reference](../../../Frutiger%20Reference/Asset/River-Surface-Flecks-User.png) identifies white flecks that read as dust/dirt. A matched underside review reproduces these marks. Disabling the surface caustic highlights removes them; disabling environment reflection does not. They are shader highlights, with no particle emitter in the river presentation.

The interior now uses Stylized Water 3's Caustics_2 texture for broader connected detail, while the accepted projected lights retain Caustics_1. Surface sampling blends the two moving layers with a softer response instead of cutting their brightest peaks into isolated fragments. The existing SmoothWaves normals, swells, reflections and refraction continue to provide water motion.

RiverGlass.asset exposes **Surface pattern opacity** (default 0.22), **Surface pattern blur** (default 3, range 0–4), **Surface pattern scale** (default 0.16 repeats/metre), and **Surface caustics**. Lower opacity reduces the pattern's light contribution without changing absorption or the visibility of objects through the glass. Increase blur to soften fine detail; filtering uses derivative-based texture mips and a trilinear repeat sampler. This does not blur scene color or the glass enclosure. The existing waterHighlights value remains an overall multiplier.

Profile presentation version 4 adds the separate surface texture and controls, preserving the original projection texture, profile GUID and references. A missing surface texture falls back to the existing caustics reference. Vendor textures/import settings remain unchanged. The two light/one cookie budget, 8 Hz projection updates, detailed-floor cap and single half-resolution background copy remain in place. Surface filtering retains two texture samples and adds no render pass or particle simulation.

Review the [original flecks](Previews/River-FlecksBefore-Underside-Close.png), [surface highlights disabled](Previews/River-FlecksNoCaustics-Underside-Close.png), and [new soft detail](Previews/River-SoftWater-Underside-Close.png). Additional captures show the [pool underside](Previews/River-SoftWater-Underside-Pool.png), [daylight surface](Previews/River-SoftWater-Above.png), [40-metre view](Previews/River-SoftWater-Distance-40-A.png), and [preserved projected wall caustics](Previews/River-SoftWater-Eye-Level-Night-A.png). RIVER_SOFTWATER_PREVIEW.json records all 64 warmed 1080p configurations, each with zero managed bytes per warmed render. Sample CPU durations are 0.97/1.27/1.49/2.03 ms above the pool and 0.65/1.57/1.48/2.39 ms in the close underside view, respectively for water disabled/surface only/projection only/combined. These are synchronous Editor render durations; GPU timing remains unavailable.

The new rendered-pixel regression verifies proportional pattern opacity, lower spatial variation with blur, retained broad detail, compatible texture fallback and the existing draw/copy budgets. Existing checks still verify motion at 20/40 metres and transparent objects through stacked water from either side.

## Rendering and local light budgets

Floors compose from far to near. Only the two nearest submitted floors receive ripple/caustic texture sampling, environment reflection, moving highlights and the separate glass pass. The mobile path limits this to one floor. Detail remains full through 42 metres and fades out by 63 metres. Additional floors retain transparent aqua absorption with no animated texture sampling or additive reflection/highlights, preventing repeated bright layers through the tower. The profile's detailedFloorLimit exposes the desktop budget (1–3, default 2).

Only the nearest eligible floor uses one additional half-resolution accumulated-color copy, normally 960 × 540 at 1080p. The mobile feature also reduces refraction weight. Regular transparent effects draw afterward. The 45-floor stress fixture submits 47 surface draws instead of 90: 45 water draws and two glass draws. These are fixture counts, not the whole scene's draw count.

RiverGlassLighting selects up to four completed visible water patches within 42 metres, targeting the nearby water and positions up to 28 metres ahead of the horizontal camera heading. Sources are separated horizontally by at least 8 metres, avoiding repeated projections at the same location on stacked floors. Each pooled patch has upward and downward shadowed cookie spotlights, with a 14-metre range and 512 px shadow. The pool still contains at most eight lights, but central scheduling allows only two running source windows and at most four enabled lights. Individual cones are approximately 81–90 degrees, with an irregular aperture, sparse bright focus points and faint flowing detail. Projectors keep a fixed orientation while fragments follow a gently meandering flow. Layer lifecycles are independent, and the entire effect occasionally fades into a complete quiet interval. All patches share one 2048 px cookie and the existing 8 Hz update budget, suspended while every light is quiet. The PC light-cookie atlas is 4096 px. The Water layer is excluded. URP honors the custom shadow-resolution tier rather than its 256 px low tier.

Cookie sampling preserves branching ridges from both texture layers and converts their RGB peaks to white light with a restrained aqua fringe. The former intersection/red-only sampling discarded most of the connected ridges and produced isolated dots. Source changes fade before relocation; rollback and hidden sources stop emission immediately. Opaque architecture occludes native projection, while completed glass apertures transmit it. Stock URP Lit receivers and the building kit receive the light. Existing AquaProjectionLamp behavior and budgets remain separate.

The shared RiverGlass profile exposes shell thickness, water bounds, tint, ripple strength/speed, white highlights, refraction/detail distances, rim/joint settings and projector controls. Its versioned authoring upgrade preserves the existing profile GUID, texture references and renderer references. Use **Tower of Babel → Environment → Apply transparent river glass pools** to reapply scene/renderer configuration; tile-repair and regeneration workflows retain this integration.

## Wider nearby projections (superseded placement)

The user's follow-up asks for the accepted projected pattern over a larger area around the player, including nearby assets at eye level. The new [reference](../../../Frutiger%20Reference/Asset/River-Wider-Caustics-User.png) is saved in the reference library and indexed.

The source range increases from 8 to 14 metres and the cone from 120 to 150 degrees. This brings low-angle nearby walls and more of the floor below overhead water into the light volume. Cookie resolution increases from 128 to 512 px, and shadow resolution from 256 to 512 px. Pattern coordinates scale with the cone footprint at a three-metre reference distance, preserving ridge size instead of stretching the old pattern across a wider area. Day/night intensities are 64/48, up from 48/36, so extended receivers remain readable. Profile presentation version 3 upgrades these settings without changing its GUID or the scene/day-night settings.

The light count stays at two, with one shared cookie at 8 Hz and the existing 24-metre source-selection limit. Detailed water remains capped at two floors; there are no extra per-floor projectors, background copies or reflection cameras. Native opaque-wall shadows still block the larger projection volume.

Current captures show [eye-level daylight](Previews/River-Wide-Eye-Level-Day.png), [eye-level night A](Previews/River-Wide-Eye-Level-Night-A.png), [night B](Previews/River-Wide-Eye-Level-Night-B.png), and [walls and pillars above the pool](Previews/River-Wide-Local-Caustics-Night-A.png). Compare the [previous 8-metre/120-degree reach](Previews/River-NarrowReach-Eye-Level-Night-A.png), which uses the same new resolution and intensity to isolate the coverage change. RIVER_WIDE_PREVIEW.json and RIVER_NARROWREACH_PREVIEW.json contain matched 1080p measurements and the settings used.

The widened fixture reports zero managed bytes per warmed render in all 56 configurations. Synchronous CPU timings in milliseconds:

| View | Water disabled | Surface only | Projection only | Combined |
| --- | ---: | ---: | ---: | ---: |
| Stacked | 0.87 | 1.21 | 1.85 | 2.18 |
| Local wall, daylight | 1.16 | 1.85 | 1.80 | 2.54 |
| Eye level, daylight | 1.34 | 1.53 | 1.65 | 2.38 |
| Eye level, night A | 1.75 | 2.04 | 1.85 | 2.80 |

These timings vary and include Editor/submission/waiting overhead; they do not establish GPU cost or unchanged gameplay FPS. Across all fourteen poses, combined-minus-disabled CPU duration ranges from approximately 0.00 to 2.01 ms. **GPU timing remains unavailable and the 2 ms GPU target is unverified.** The wider range includes more nearby receivers/casters and the higher-resolution cookie/shadows use more pixels, while the number of sources and updates remains bounded.

Rendered receiver checks now include walls five metres away at 1.8-metre player height, walls nine metres away, and horizontal floors/ceilings nine metres sideways and six metres vertically from the source. They verify illumination, moving pixels and obstruction by solid geometry above and below the pool. The extended receivers are also compared against an 8-metre light range, confirming the coverage increase on actual surfaces.

## Higher projection resolution — 2026-10-25

The follow-up resolution increase raises the shared projection cookie from 512 × 512 to 2048 × 2048 and the PC light-cookie atlas from 2048 × 2048 to 4096 × 4096. Projection range, cone, brightness, shadow resolution, two-light limit and 8 Hz updates are unchanged. The Mobile atlas retains its existing 1024 px budget, so mobile rendering may downsample the cookie.

Matched Edit Mode reviews show [512 px before](Previews/River-Resolution512-Local-Caustics-Night-A.png) and [2048 px after](Previews/River-Resolution2048-Local-Caustics-Night-A.png). The new capture preserves finer branching ridges and reduces broad smearing on the wall and pillar. RIVER_RESOLUTION512_PREVIEW.json and RIVER_RESOLUTION2048_PREVIEW.json record daylight and two night poses, with two lights and one shared cookie in each combined view. These are comparison fixtures, not a reproduction of the player's exact camera.

The ARGB32 projection texture increases from approximately 1 MiB to 16 MiB, with additional atlas memory and more pixels per cookie update. GPU cost remains unverified. C# compilation passed; the previews restored the clean Tower scene without entering Play Mode. The existing resolution assertion was updated; automated tests were not run under the user's standing preference against unrequested automated tests. Earlier automated-test totals below are historical.

## Collections of smaller projections — 2026-10-25

The user clarified that broader coverage means a collection of smaller effects extending ahead of the player. Enlarging a single nearby cone was the wrong placement model. Profile presentation version 5 adds a four-patch pool, 28-metre forward targets, 42-metre source selection, 8-metre minimum horizontal spacing and independent pattern density. The cone narrows from 150 to 110 degrees; density 2.5 makes the projected branches approximately 40% of their previous world size at the same receiver distance. Actual coverage follows available completed water sections rather than projecting from dry or unfinished tiles.

Selection follows the camera's horizontal heading, with a stable heading fallback when looking vertically. Existing-source preference and fade-before-relocation remain in place; hidden or rolled-back sources stop immediately. Shadows continue to block light through opaque geometry. The pool raises the maximum shadowed-light count from two to eight; all lights share one 2048 px texture, one material and one 8 Hz update, so the previous resolution increase is retained without allocating a texture per patch. There are no extra refraction copies or detailed water floors.

The disposable ceiling/pillar review shows [one large nearby effect before](Previews/River-SinglePatch-Ahead-Night-A.png), [smaller patches extending ahead after](Previews/River-PatchCollection-Ahead-Night-A.png), and [coverage after turning around](Previews/River-PatchCollection-Ahead-Turned-Night.png). Daylight and a second animation time are also saved. RIVER_SINGLEPATCH_PREVIEW.json and RIVER_PATCHCOLLECTION_PREVIEW.json record positions, counts and warmed render measurements. The new forward views use four source positions extending roughly 28 metres ahead; turned views select water on the opposite side. These are controlled comparison fixtures rather than the player's exact saved camera.

All 20 new warmed configurations report zero managed bytes per render. Combined synchronous CPU render durations range from approximately 2.08 to 3.39 ms across the five views. These Editor measurements include submission and waiting; GPU timings and gameplay FPS remain unverified. More shadowed lights increase potential rendering cost even though cookie allocation and animation work remain shared.

C# compilation passed and the cookie shader reported no diagnostics. Visual review stayed in Edit Mode and restored the clean Tower scene with no pooled lights left behind. Existing pool and native-receiver checks were updated for distributed sources and narrower individual cones. Automated tests were not run under the user's standing preference against unrequested automated tests; previous automated-test results are historical.

## Smaller, subtler pattern — 2026-10-25

The next user adjustment reduces the effect itself while retaining the distributed coverage. Profile presentation version 6 raises pattern density from 2.5 to 5, approximately halving the previous branch size. Day/night light intensities decrease from 64/48 to 20/12 (about 69%/75% lower). The four patch positions, forward targets, ranges, shadow settings and shared 2048 px texture remain as configured above.

The [subtle night preview](Previews/River-SubtlePatches-Ahead-Night-A.png) and [daylight preview](Previews/River-SubtlePatches-Ahead-Day.png) show the smaller detail across the same ceiling and pillar fixture. RIVER_SUBTLEPATCHES_PREVIEW.json records the five views and current settings. C# compilation and Edit Mode visual inspection completed; automated tests were not run under the standing preference. The clean Tower scene was restored without entering Play Mode.

## Intermittent irregular reflected glints — 2026-10-25

The latest request replaces continuously visible circular coverage with intermittent, irregular reflected light on nearby assets. Profile presentation version 7 adds independently randomized 1.4–3.2-second bursts, 2.5–6-second quiet intervals and 0.55-second edge fades. Burst brightness, orientation and footprint vary independently for each source. The random generator is local to each patch and does not alter Unity's global random state. Timers persist when moving between source tiles, so walking does not continually restart the delay. Construction rollback and hidden sources still stop emission immediately.

The cookie now combines three differently rotated, nonmatching texture scales with procedural domain warping. Drifting noise cuts holes into a torn, softly faded outline. Its former constant illumination floor is removed. Narrow white ridges and concentrated glints use near-white HDR light with peak day/night intensities 144/96, scaled by the burst envelope and per-burst variation; these values supersede the constant 20/12 tuning above. The small pattern scale, four-patch distribution, shared 2048 px texture and native opaque-wall occlusion remain in place. Additional procedural work and the third texture sample have not been GPU-profiled.

Projection masks explicitly exclude this project's Water layer (4), confirmed on the live ocean renderer. Custom transparent river water already samples only sunlight/environment rather than these additional lights. Architecture can still be seen illuminated through transparent water; that is a receiver behind the water, rather than direct water lighting.

The six-moment sequence records [quiet](Previews/River-IrregularBursts-Bursts-Quiet.png), [first visible group](Previews/River-IrregularBursts-Bursts-B.png), [a different burst combination](Previews/River-IrregularBursts-Bursts-C.png), and [quiet again](Previews/River-IrregularBursts-Bursts-D.png). Its active-light counts at 0.1, 2, 4, 6, 8 and 11 seconds are 0, 0, 4, 8, 0 and 4; all share one allocated cookie. RIVER_IRREGULARBURSTS_PREVIEW.json records the moments, intensities, culling masks and warmed renders. The daylight/night/turned review is saved as RIVER_IRREGULARGLINTS_PREVIEW.json with corresponding images. Review now advances the real burst scheduler and preserves sampled light-enabled states instead of forcing quiet lights back on.

C# compilation and shader diagnostics are clear. The 24 sequence configurations reported zero managed bytes per warmed render. Render timings exclude the scheduler/cookie-generation steps performed before measurement; GPU timing and gameplay FPS remain unverified. Review stayed in Edit Mode and restored the clean Tower scene. Existing pool/native-receiver checks were updated for quiet intervals, burst timing, varying cones and water exclusion; automated tests were not run under the standing preference against unrequested automated tests.

## Small, subtle weaving motion — 2026-10-25

The user rejected the large, visibly popping glints. Profile presentation version 8 narrows the maximum cone from 110 to 60 degrees and shrinks the cookie's irregular aperture. At a comparable height, the visible patch is roughly 30% of the previous diameter. Day/night peaks drop from 144/96 to 32/20, with per-cycle multipliers capped at 1 instead of 1.25. The shared 2048 px detail, four-source distribution, water exclusion and native shadows remain.

Footprints now glide in a continuous two-axis weave, with gentle continuous roll. Each patch keeps its own phase and orientation across brightness cycles; the previous random rotation at every burst start is removed. Sources remain anchored in completed water. Lateral drift is calibrated to approximately one metre at a three-metre receiver height, at an angular rate of 0.35 radians/second. Larger receiver distances increase the visible travel proportionally.

Active cycles last 7–11 seconds with 2.8-second fades, separated by 3–7-second quiet periods. Source handoffs fade over 1.6 seconds instead of the former 0.2 seconds. Moving between tiles continues to preserve the independent clock. Hidden or rolled-back water still stops immediately, even during a fade.

The [18-second motion review](Previews/River-Weaving-Motion.gif) records 180 actual Edit Mode renders at 960 × 540 and 10 frames/second. [Night still](Previews/River-Weaving-Weave-Night.png) and [day still](Previews/River-Weaving-Weave-Day.png) show the smaller footprint. RIVER_WEAVING_MOTION.json records per-frame light directions and intensities; RIVER_WEAVING_PREVIEW.json records the fixed-view configuration. The recorded largest direction step is approximately 0.75 degrees per 0.1 second, with an intensity step of approximately 1.07. This is a stationary-camera fixture, not a gameplay performance measurement.

C# compilation and shader diagnostics are clear. Visual frames and motion records were inspected, and the clean Tower scene was restored without entering Play Mode. Existing cone, receiver-range and scheduler expectations were updated; automated tests were not run under the standing preference. GPU cost remains unprofiled.

## Luminous sliding cutouts — 2026-10-25

The latest request increases emission while replacing self-rotation with pieces flowing out and new pieces entering. Profile presentation version 9 raises day/night light peaks from 32/20 to 96/64, approximately three times the prior light output. The small 60-degree cone, compact aperture, near-white tint and long fades are retained. This boosts actual native HDR light on receivers; it does not change every asset's material emission or the global bloom settings.

Projector direction and roll now remain fixed for each water source. The previous aiming weave and animated roll are removed. Instead, an unbounded, warped cutout field translates through the stationary soft window at 0.06 texture UV/second. Fragments fade through the edges as new portions of the field arrive. Each source retains a different fixed orientation, giving neighboring patches different slide directions without spinning them. The old projector-drift controls are replaced with cutout speed and warp controls. Water exclusion, source validation and opaque-wall blocking remain unchanged.

The [18-second sliding-cutout preview](Previews/River-Sliding-Cutouts-Motion.gif) contains 180 Edit Mode frames at 960 × 540. RIVER_SLIDINGCUTOUTS_MOTION.json records zero change in projector direction while the cutout field travels 1.074 UV across the recording. [Night still](Previews/River-SlidingCutouts-Weave-Night.png) and [day still](Previews/River-SlidingCutouts-Weave-Day.png) accompany RIVER_SLIDINGCUTOUTS_PREVIEW.json. Frames at different times were inspected to check the stronger glints and changing cutout fragments.

C# compilation and shader diagnostics are clear. The existing pool check now also requires stationary projector orientation while cookie pixels change. Automated tests were not run under the standing preference. The clean Tower scene was restored without entering Play Mode; GPU cost remains unprofiled.

## Broader scale with bright points and faint detail — 2026-10-25

The latest request scales up the effect while separating high-opacity bright points from low-opacity detail. Profile presentation version 10 increases the maximum cone from 60 to 90 degrees, approximately 1.7 times the footprint diameter at the same receiver height. Pattern density decreases from 5 to 2.5, doubling the world size of individual pattern features rather than packing more small marks into the enlarged area.

The cookie has two independently controlled visual contributions: sparse focus points at highlight opacity 0.95, and broad flowing branches at detail opacity 0.06. These controls govern relative projected-light contribution rather than a receiver material's transparency. A separate noise gate breaks bright ridges into sparse points. Bright points retain strong intensity inside valid cutouts, with soft edge fading; the faint detail retains the full low-opacity aperture. Both share the existing sliding cutout field, with no extra light pair, texture or render pass. Native day/night intensity remains 96/64; water exclusion, fixed orientations and long fades are unchanged.

The [18-second composite motion preview](Previews/River-Layered-Scale-Motion.gif) and [composite still](Previews/River-LayeredScale-Weave-Night.png) show the wider distribution. [Bright points alone](Previews/River-BrightPointsOnly-Weave-Night.png) and [faint detail alone](Previews/River-FaintDetailOnly-Weave-Night.png) were captured by temporarily isolating each contribution and restoring the shared profile afterward. RIVER_LAYEREDSCALE_PREVIEW.json and RIVER_LAYEREDSCALE_MOTION.json record the final configuration; the motion log retains zero projector-direction change while cutouts travel 1.074 UV over 180 frames. Separate layer reports use RIVER_BRIGHTPOINTSONLY_PREVIEW.json and RIVER_FAINTDETAILONLY_PREVIEW.json.

C# compilation and shader diagnostics are clear. The cone-budget expectation was updated; automated tests were not run under the standing preference. Edit Mode review restored the clean Tower scene and final 0.95/0.06 profile values. GPU cost remains unprofiled.

## Natural timing, meandering flow and independent layers — 2026-10-25

The user approved and requested implementation of the natural-reflection plan. Profile presentation version 11 adds central appearance scheduling and independent layer lifecycles, preserving the accepted 90-degree footprint, density 2.5, 0.95/0.06 highlight/detail contributions, 96/64 day/night peaks and shared 2048 px texture.

Appearance windows last 6–9 seconds with 2–4-second individual rests. No more than two windows run concurrently, including their initially invisible fade frame and source handoffs. Eligible starts are staggered by a randomized 1.2–2.5-second spacing, with priority given to the earliest waiting source. Actual gaps can be longer while capacity is full or the entire effect is resting. Patch fades remain 2.8 seconds, and relocation fades remain 1.6 seconds. Tile handoffs retain the running clock; invalid or hidden sources still stop immediately.

After 14–20 seconds of normal operation, the effect fades out for 1.5 seconds, holds completely quiet for 1–2 seconds, and fades back for 1.5 seconds. New windows are held during those transitions, then resume with staggered starts. Existing windows keep advancing through the pause. Scheduler randomness is local and does not modify Unity's global random state; missed pauses after a large clock jump are skipped rather than replayed.

The forward flow retains its 0.06 UV/second base speed, smoothly varying by ±15%, with sideways displacement bounded by 0.035 UV. It never spins or reverses its general forward direction. The flow offset is computed once in C# and passed to the existing cookie shader; its clock continues through quiet periods. Faint detail swells over 6–10 seconds. Spatially varied bright-point lifetimes span 2–4 seconds, with fades of at least 0.7 seconds and a small gap between local lifecycles. Neighboring lifecycle cells are smoothly blended so sliding pieces do not encounter hard cell boundaries. The two layers use different coordinates and timing ranges.

Public profile controls expose the active-source limit, start spacing, full quiet intervals/duration/fade, flow variation/sway and layer lifetimes/fade. Read-only runtime diagnostics expose running-window count, global visibility, window starts and the current flow offset. No extra light, texture, material or render pass was added.

### Review evidence

- [36-second stationary motion](Previews/River-Natural-Reflections-Motion.gif) and [36-second stationary/walking/turning capture](Previews/River-Natural-Reflections-Navigation.gif), each with 360 frames at 960 × 540 and 10 fps. Navigation contains 12 seconds each of standing, walking and turning; the walking path travels 20 metres and the turn reaches 180 degrees.
- RIVER_NATURALSTATIONARY_MOTION.json and RIVER_NATURALNAVIGATION_MOTION.json record source windows, quiet visibility, flow, camera movement, source positions, light masks and rotations. Both recorded a maximum of two running windows, four enabled lights, one cookie and zero projector rotation. Navigation recorded 39 source relocations without resetting the flow clock.
- The stationary capture sampled a complete quiet interval from 20.9 through 21.9 seconds, followed by the gradual return. Forward flow ranged from approximately 0.051 to 0.069 UV/second, and sideways displacement reached approximately 0.035 UV, matching the planned bounds.
- [Clear native-light receiver](Previews/River-NaturalOcclusion-Occlusion-Clear.png) versus [the same frame with a solid shadow-casting blocker](Previews/River-NaturalOcclusion-Occlusion-Blocked.png). The controlled views differ at 1,494 pixels by more than three byte levels; visible reflected light disappears behind the blocker. The slab is confined to the disposable review scene.
- NaturalHighlightsOnly and NaturalDetailsOnly captures isolate each layer at several moments, restoring the accepted profile values afterward. NATURAL_REFLECTION_REVIEW.json aggregates the observations and artifact paths.

C# compilation and shader diagnostics are clear. Existing pool/scheduler expectations now cover the two-window/four-light limit, staggered starts, full quiet interval and fixed projector orientation; receiver checks observe a real scheduled window rather than assuming an always-on sample time. As explicitly requested in the plan, no automated tests were created or run. Review stayed in Edit Mode and restored the clean Tower scene, with no pooled cookie left allocated. These are controlled fixture views rather than gameplay verification. GPU cost remains unprofiled; warmed render timings exclude scheduler/cookie updates performed before measurement.

## Measurements after the gameplay repair and stacking cap (before wider projections)

RIVER_BUDGET_PREVIEW.json records fresh warmed 1920 × 1080 Edit Mode samples with the same comparison cameras, quality, lighting and construction fixture. Each configuration warms for eight renders and measures twelve. All 44 samples report zero managed bytes per warmed render. The three-floor fixture uses five surface draws, at most one half-resolution background copy, and at most the existing two shadowed river lights sharing one cookie. No decal projector or additional reflection camera was introduced; accurate opaque-wall blocking was retained at the user's request.

| View | Water disabled | Surface only | Projection only | Combined |
| --- | ---: | ---: | ---: | ---: |
| Above | 1.31 ms | 1.26 ms | 1.92 ms | 2.06 ms |
| Below | 1.91 ms | 2.00 ms | 1.81 ms | 1.98 ms |
| Stacked | 1.22 ms | 1.73 ms | 1.89 ms | 2.46 ms |
| Ring join | 1.44 ms | 1.81 ms | 1.50 ms | 1.74 ms |
| Night A | 1.30 ms | 1.72 ms | 1.44 ms | 2.34 ms |
| Night B | 1.72 ms | 2.16 ms | 2.14 ms | 3.04 ms |

These are synchronous CPU render durations, including Editor/submission/waiting overhead, and vary between runs. The combined-minus-disabled difference across all eleven poses ranges from approximately 0.07 to 1.32 ms. GPU durations remain unavailable; this does not establish the 2 ms GPU target or gameplay FPS.

Current captures: [daylight](Previews/River-Budget-Above.png), [underside](Previews/River-Budget-Below.png), [stacked transparency](Previews/River-Budget-Stacked.png), [40-metre animation A](Previews/River-Budget-Distance-40-A.png), [animation B](Previews/River-Budget-Distance-40-B.png), [night caustics on real receivers A](Previews/River-Budget-Local-Caustics-Night-A.png), and [B](Previews/River-Budget-Local-Caustics-Night-B.png). The stacking regression also compares rendered pixels against water disabled, verifies that an additional distant floor keeps its tint without animation, and confirms that the nearest pool still moves.

## Visual review and historical measurements

The comparison timings and captures below describe the earlier enhancement before the gameplay regression repair. They are retained as historical evidence and must not be treated as a performance verdict for the repaired full-tower path.

RiverGlassVisualReview.Capture("Enhanced") creates disposable Edit Mode views at 1920 × 1080, restores scene/light/render settings afterward, and writes the enhanced PNGs and RIVER_ENHANCED_PREVIEW.json beside these notes. RIVER_BASELINE_PREVIEW.json records the implementation before the rendering changes. Each pose warms for eight renders and measures twelve, separating disabled, surface-only, projection-only and combined configurations.

The six original comparison views retain matched cameras and fixture geometry. Additional 40-metre motion views and local wall/pillar reviews are recorded separately. Local projection views use proper spatial chunks; the original synthetic comparison fixture placed a complete floor in one artificial chunk, whose bounds are unsuitable for proving nearby projector placement. The saved tower itself uses spatial chunks. The tower review images use saved transforms with completed preview floors; they do not change actual construction progress.

Measured CPU duration of the combined synchronous Edit Mode render, in milliseconds:

| View | Previous | Enhanced |
| --- | ---: | ---: |
| Below | 3.34 | 2.83 |
| Stacked | 3.61 | 2.81 |
| Ring join | 4.23 | 3.18 |
| Night A | 4.64 | 3.66 |
| Night B | 5.50 | 3.89 |

The baseline Above result contains an extreme 1991 ms warmup/compilation outlier and is excluded from improvement claims; its raw value is retained. The enhanced Above result is 2.51 ms. These timings include Editor submission and GPU waiting overhead, vary between runs, and are not gameplay FPS or isolated GPU costs.

The six enhanced comparison views measured approximately 0.20–1.34 ms more CPU render duration than their disabled configuration and zero managed bytes per warmed render. They use at most one extra half-resolution copy, two river lights and one cookie. A source outside selection range produces zero lights.

**The 2 ms GPU acceptance target remains unverified.** FrameTimingManager/get_performance returned a GPU duration of zero in this Editor session; the reports record unavailable GPU timing as null. A representative player GPU profile is still needed to establish that target.

Useful captures:

- [Above the pool](Previews/River-Enhanced-Above.png), [below the pool](Previews/River-Enhanced-Below.png), and [stacked floors](Previews/River-Enhanced-Stacked.png).
- [White light on a wall and pillar](Previews/River-Enhanced-Local-Caustics-Night-A.png) and [another animation time](Previews/River-Enhanced-Local-Caustics-Night-B.png).
- [Water at 40 metres](Previews/River-Enhanced-Distance-40-A.png) and [another animation time](Previews/River-Enhanced-Distance-40-B.png).
- [Saved tower geometry and channel junction](../Buildings/Previews/River-Enhanced-Floor0-Joins.png), [interior](../Buildings/Previews/River-Enhanced-Interior.png), and [underside](../Buildings/Previews/River-Enhanced-Floor0-Underside.png).

## Verification

RiverPoolVerification.Start() runs the scoped EditMode checks through Unity's TestRunnerApi; Start(true) runs the final PlayMode checks. Result files are RiverPool-EditMode-Results.json and RiverPool-PlayMode-Results.json.

Final repair, projection and surface-detail results: **42 EditMode checks passed, 15 PlayMode checks passed, no failures or skips.** The GPU regression submits 19,125 instances across 45 floors using an unreadable shared source mesh, proving rendering does not read or expand its vertices. It also verifies the two-floor detail cap and 47-draw bound. Additional coverage includes surface opacity/filtering, compatible texture fallback, local-only projection shadows, channel masks, transparency from either side through multiple floors, half-resolution/single-copy limits, motion at 20/40 metres, floor identity, batching, native receiver illumination/animation, opaque-wall occlusion, completion/rollback, runtime cleanup, actual full-tower build/reset, and interaction after completing the first floor. The separate geometry-builder checks exercise the authoring helper only.

C# compilation and the affected shader diagnostics were clear. Implementation and visual reviews occurred in Edit Mode. Play Mode was entered only through the automated runner after implementation, and the original Tower scene was restored.
