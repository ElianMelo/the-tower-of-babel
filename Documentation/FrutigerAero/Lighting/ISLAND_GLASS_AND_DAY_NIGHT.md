# Island, glass rivers and the tower atmosphere

The 2026-10-24 blueprint is the current glass-floor layout: two concentric water channels at 50% and 88% of the floor's wall radius, four straight cardinal spokes, and a central pool at 16%. This supersedes the provisional outer-ring-only interpretation. Ordinary architecture remains opaque white with blue glazing.

## Glass construction

`GlassWaterFloor.prefab` is a composite floor tile with exactly the ordinary tile's 4 × 4 m footprint and top datum. A sealed 20 cm slab contains the apparent water bed beneath a polished glass surface. The editable Blender collection is `ArtSource/Buildings/GlassWaterFloor.blend`; the source FBX and Unity mesh bake are beside the existing building models. The mesh has 1,152 triangles before radial bending. Blender's open sand/glass workshop was preserved.

The layout selects every standard tile touched by a channel. Its shared material draws continuous circular boundaries and straight spokes in world space, including the small white parts of a composite tile. The existing 87,972 construction assets remain; 19,114 are now glass composite tiles. Pillar support bridges and stair clearances retain their white structural tiles. Positions, colliders, stages and floor completion rules are retained; local cache indices are rebuilt deterministically for the new asset type.

The new asset uses **glass**, stages 0–10, the existing incoming-chunk construction animation, near-only blueprints, and instanced distant built stages. It remains solid and non-interactable at stage 10. Water flow, ripple normals and the apparent refracted bed share world coordinates across adjacent tiles. Reflection uses Fresnel and the project's aquatic environment cubemap; this is an environment reflection, not a live planar mirror. The enclosed bed occludes correctly beneath stacked floors.

## Caustics

The lake, glass bed and artificial projection cookie now use the installed Stylized Water 3 caustic texture. Project shaders include the installed package's `Caustics.hlsl` sampling function rather than duplicating vendor code. Its two moving layers replace the previous procedural pattern. The existing source-bounded depth projection is retained as the receiver mechanism for ordinary surfaces; this is not the optional underwater extension.

Lake footprints retain their shore and vertical limits. Only completed glass panels register a small nearby light footprint; blueprints, partial panels, rollback and pool release do not emit caustics. The nearest eight water regions can contribute, and the camera distance remains capped at 42 m. AquaProjectionLamp keeps its native spotlight cookie, four-light budget, 128 px cookie and 8 Hz animation. No paid extension or new package was installed.

## Sunlight, volume and fireflies

One reusable Particle System provides 48 crossed transparent beam sheets around the current floor. Entry points snap to arch openings; each beam has a controlled inward direction and downward angle, a soft shape, depth intersection fade and a stable low-frequency shimmer. The beam's endpoint stays above the walking floor and its start stays beneath the roof. Distant emitters fade between 55 and 80 m. Two short-range, shadow-free spotlights provide nearby side bounce, rather than one light per beam.

The existing half-resolution, shadow-sampled sun scattering pass is retained. `TowerExitAtmosphere.asset` adds local bloom/color treatment and a custom `AeroShaftVolume` override that raises scattering intensity/density. The volume follows the viewed floor; its weight rises near the open circumference and is zero outside the tower's vertical range. The particle beam strength rises with the same exit proximity.

Fireflies use one camera-local box emitter: maximum 96, size 0.018–0.055 m, 8–16 second lifetimes, randomized aqua/green start colors, soft alpha/color grading, and low-frequency single-octave noise with a gentle upward drift. Changing floors clears the old population. They do not allocate one real light per particle. The reusable source is `Assets/_Project/Prefabs/Lighting/AeroFireflies.prefab`.

The sun uses an occluded two-element SRP lens flare. A restrained screen-space flare override also responds to bright highlights. Both renderer paths are enabled. The SRP sun flare fades out at night; it does not draw through the covered roof.

## Day and night controls

Select `Assets/_Project/Environment/Lighting/AeroDayNight.asset` to tune the cycle. The default is 20 minutes, starting at 0.32 of a day. Controls include duration, automatic advancement, sun azimuth/intensity/colors, moon intensity/color, ambient day/night colors, sky exposure, sun flare strength, beam strength/length/downward angle, exit volume strength and firefly density.

`Aero island atmosphere` holds the cycle and effects. Runtime time is exposed as `timeOfDay` (0 = midnight, .25 = sunrise, .5 = midday, .75 = sunset). Time can be paused through the profile. Sun/moon lighting, ambient spherical harmonics, reflections, fog color, sky exposure, beam tint and flare intensity respond to the clock. The cycle owns one temporary runtime sky material and restores the saved sky reference on disable; it does not mutate the sky asset.

## Island and performance

The terrain has an asymmetric coast around an approximately 350 m radius, rolling inland hills, the original level tower foundation and three lakes. Every terrain boundary is below sea level. Deep seabed patches are cut out so the original square terrain cannot show through the distant ocean; shallow coastal shelves and lake bottoms remain. Marker renderers are hidden while their transforms remain available to the terrain authoring tool.

The existing single seamless ocean mesh remains. Terrain resolutions stay at height 513, splat 256 and grass 512. Editor/runtime grass budgets remain separate; interpolated shoreline height excludes grass from water. The original source TerrainData remains untouched.

## Research used

- Supplied **Introduction to the Universal Render Pipeline for advanced Unity creators, Unity 6 edition**, pp. 79–87 and 140–144: transparent halo shaping, SRP flare assets, screen-space flares, and view/normal-based Fresnel. The PDF's indexed lens-flare links were followed into the Unity 6 manual.
- Supplied **The Unity Shaders Bible**, pp. 238–246: world-space view direction and surface normals for Fresnel. The useful optical principle was adapted to URP; its older Built-in shader boilerplate was not copied.
- [Unity 6 URP lighting overview](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lighting-landing.html), [reflections](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lighting/reflection-probes.html), [SRP lens flare component](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shared/lens-flare/lens-flare-srp-reference.html), [flare data](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shared/lens-flare/lens-flare-asset.html), [screen-space flares](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shared/lens-flare/post-processing-screen-space-lens-flare.html), and [particle noise](https://docs.unity3d.com/6000.0/Documentation/Manual/PartSysNoiseModule.html).
- [NVIDIA GPU Gems 3, chapter 13](https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process): screen-space radial sampling has off-screen and foreground-occlusion limitations. This informed using depth-tested local geometry for consistent side beams, with shadow scattering as a separate restrained layer.
- [Staggart's Stylized Water 3 documentation index](https://staggart.xyz/unity/stylized-water-3/): the documentation endpoint failed to load during research, so implementation details were verified directly against the installed licensed package's shader libraries, ForwardPass and WaterObject code. Vendor source is not reproduced in this document.

## Review artifacts

- `Previews/Glass-River-Blueprint.png`: full floor from above.
- `Previews/Island-Overview.png`: surrounding ocean and irregular coast.
- `Previews/Tower-Rays-0.png`, `90.png`, `180.png`, `270.png`: covered interior from four sides.
- `Previews/Tower-Night.png`: nighttime lighting.
- `IslandLightingValidation.json`: final compiler/test/editor-state evidence.

These previews use completed copies of saved construction geometry in a temporary review scene; they do not complete the player's tower. Island captures show terrain in Edit Mode, where the cached construction geometry is not materialized.

Final verification: **47 Edit Mode and 14 Play Mode tests passed**, with zero failures/skips and no shader compiler errors. Play Mode was entered only by the automated runner after implementation, then restored to the clean Tower scene in Edit Mode. Runtime checks include cycle advancement, sun/moon/flare switching, particle motion/budget, caustic animation/range, completed-only glass projection, pool release/rollback and floor progression. The standalone Blender source was reopened in a separate background process and contains one mesh with no linked objects. GPU frame time has not been profiled.
