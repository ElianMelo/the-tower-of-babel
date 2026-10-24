# Render Graph review

## 1. Validation summary

Reviewed the two project-owned lighting features against Unity 6000.3.11f1 and the installed URP sources. Lake projection is source-bounded; sun scattering uses native shadows and covered side apertures. Both use explicit graph dependencies and static execution callbacks.

## 2. Confirmed issues

The previous camera-wide caustic projection had no water/source mask. Its derivative normal crossed sky silhouettes and could generate invalid reflection coordinates. The old sun presentation relied on roof openings and an elevated sun. Depth-reconstructed screen-space AO added dark coplanar tile lines in the neutral-material comparison.

## 3. Likely issues / risky assumptions

Lake footprints approximate the generated shoreline with rotated ellipses and a small feather. They are not a general arbitrary-water-shape solution. Sun detail depends on the existing directional shadow map and its 50-metre range. Screenshots review fixed views rather than asserting every possible camera position or player-build frame rate. Direct3D 12 suffered a device crash during diagnosis; subsequent verification used Direct3D 11.

## 4. Recommended fixes applied

Registered lake footprints now bound projection horizontally and vertically, with source/sky/range rejection before texture work. Terrain basin angles are converted to the opposite Unity yaw and checked against saved basin data. Five-tap same-surface reconstruction, safe normalization and finite nonnegative light guards replace unstable derivatives. Plain architectural stone no longer self-emits moving water patterns. The faulty AO pass is disabled; baked shading and native shadows remain. Low sunlight, stronger lateral scattering and restrained sky contribution produce side beams beneath complete floors.

## 5. Corrected implementation

`AeroLightRendererFeature` assigns source count and all used array entries each recording; entries beyond the count are not consumed. The immutable pattern/cubemap and parameters are explicit material bindings; graph depth is explicitly bound by Blitter. Existing color is read/write for blending, with no extra render target. Regions are capped at eight; this scene has three.

`AeroSunShaftRendererFeature` reads graph depth and native main-light shadows, derives its half-resolution RGBA16F descriptor from active color, and writes one temporary target. A separate bilateral composite reads that target and full depth, then blends into read/write active color. Every graph handle and parameter is assigned per recording. Auxiliary bindings occur during static execution; no graph textures are published globally. Custom raster passes are appropriate for these integration/blending operations.

## 6. Missing information

A representative player-build GPU profile and Direct3D 12 driver/device investigation remain necessary for platform-specific performance or stability claims. No numerical FPS claim is made. Current automated and visual results are in `LightingRepairValidation.json`.
