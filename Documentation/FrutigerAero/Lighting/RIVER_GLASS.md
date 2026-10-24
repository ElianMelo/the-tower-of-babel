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

Installed Stylized Water 3 SmoothWaves and Caustics_1 assets are referenced without modifying vendor files. Reflections use the existing environment and sun; there is no extra reflection camera or live character reflection.

## Rendering and local light budgets

Floors compose from far to near. Farther floors use transparent absorption and reflection. Only the nearest eligible floor uses one additional half-resolution accumulated-color copy, normally 960 × 540 at 1080p. The mobile feature reduces refraction weight. Regular transparent effects draw afterward.

RiverGlassLighting selects one completed visible source patch within 24 metres. It supplies two 120-degree shadowed cookie spotlights, upward and downward, each with an 8-metre range and 256 px shadow. Their shared 128 px cookie advances at 8 Hz. The narrower cones use a broad inner angle and a soft outer cookie aperture so nearby walls do not fall into compounded dark edge fades.

Cookie sampling preserves branching ridges from both texture layers and converts their RGB peaks to white light with a restrained aqua fringe. The former intersection/red-only sampling discarded most of the connected ridges and produced isolated dots. Source changes fade before relocation; rollback and hidden sources stop emission immediately. Opaque architecture occludes native projection, while completed glass apertures transmit it. Stock URP Lit receivers and the building kit receive the light. Existing AquaProjectionLamp behavior and budgets remain separate.

The shared RiverGlass profile exposes shell thickness, water bounds, tint, ripple strength/speed, white highlights, refraction/detail distances, rim/joint settings and projector controls. Its versioned authoring upgrade preserves the existing profile GUID, texture references and renderer references. Use **Tower of Babel → Environment → Apply transparent river glass pools** to reapply scene/renderer configuration; tile-repair and regeneration workflows retain this integration.

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

Final repair results: **34 EditMode checks passed, 15 PlayMode checks passed, no failures or skips.** The GPU regression submits 19,125 instances across 45 floors using an unreadable shared source mesh, proving rendering does not read or expand its vertices. Additional coverage includes local-only projection shadows, channel masks, transparency from either side through multiple floors, half-resolution/single-copy limits, motion at 20/40 metres, floor identity, batching, native receiver illumination/animation, opaque-wall occlusion, completion/rollback, runtime cleanup, actual full-tower build/reset, and interaction after completing the first floor. The separate geometry-builder checks exercise the authoring helper only.

C# compilation and the affected shader diagnostics were clear. Implementation and visual reviews occurred in Edit Mode. Play Mode was entered only through the automated runner after implementation, and the original Tower scene was restored.
