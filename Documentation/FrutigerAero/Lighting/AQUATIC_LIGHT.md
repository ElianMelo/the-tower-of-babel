# Nearby aquatic light

Based on the user's [aquarium interior](../../../Frutiger%20Reference/Asset/Aquatic-Light-Interior-User.png) and [caustic detail](../../../Frutiger%20Reference/Asset/Aquatic-Light-Caustics-User.png), supplied on 2026-10-23. Preserve the white architecture and aqua glazing; carry the moving water light onto surrounding surfaces.

## In the game

- Moving, overlapping caustic strands project only within actual lake footprints and their immediate banks. Three `AeroWaterLightRegion` components follow the saved lake centers, radii, rotation and water elevation. Four metres of shore fade and vertical limits (three metres above, five below) prevent light reaching unrelated floors or dry terrain. Projection remains anchored in world space.
- Soft cyan reflected light, sun glints and a prefiltered aquatic HDR environment reflection add the glossy Frutiger Aero finish. They share the same range and fade as the caustics.
- Camera distance adds a 24–42 metre fade inside those source regions. The whole pass is skipped when no lake region is nearby. Camera movement never creates a source. Artificial sources use the separate native spotlight-cookie system with a ten-metre range.
- The lake retains the existing water package's live screen-space reflections and underwater caustics. Reduced distortion and a narrower foam edge make the reflections easier to see.
- UI is drawn afterward. Transparent water and glass retain their own reflection and transmission shading; the global depth projection affects opaque receiving surfaces.

This is an art-directed lighting treatment. The shared HDR environment supplies glossy highlights; it does not capture moving off-screen objects. Water screen-space reflections can reflect geometry currently available in the view, with the existing environment reflection as fallback.

The later [sun rays and projection lamps](SUN_RAYS_AND_PROJECTION_LAMPS.md) are separate systems with their own bounded rendering costs and validation. The one-pass/no-extra-target budget below describes this nearby surface-light feature.

## Cost and settings

`Assets/Settings/PC_Renderer.asset` contains **Aero nearby water light**. Its settings control distance, fade start, caustic intensity, reflected light, scale, speed and tint. Setting both strengths to zero skips the pass. The material uses the existing `Aero_WaterCaustics.png` and `Review_AquaticReflection.exr`; no generated texture is updated per frame.

One native Render Graph raster pass samples existing depth and blends into the opaque color attachment. There is no extra color copy, light render target or reflection camera. A bounded registry supplies up to eight source footprints; the scene currently uses three. Source, sky and distance rejection happen before caustic texture work. A five-tap surface-normal reconstruction chooses same-surface neighbours, and finite/nonnegative output guards prevent the old black/white silhouette artifacts from entering bloom. The pass runs before skybox/opaque-color copying so water reflections can include local lighting.

The existing water height prepass is no longer always requested at its former 4096 resolution. The water package can still request it automatically if a height-query consumer needs it. Terrain resolutions, grass density/distance budgets, construction visibility, meshes and colliders are preserved.

Water tuning lives in `Assets/_Project/Environment/AeroLakeWater.mat`, a project-owned copy. The third-party source material and shader are untouched. `AeroWaterLightAuthoring.Apply()` installs/configures the effect through Unity's asset APIs in the saved Tower scene.

## Verification and previews

`AeroLightEditModeTests` render the actual URP pass to check source exclusion, upper-floor exclusion, nonnegative silhouette pixels, unchanged interface pixels, ordinary materials, reflections, animation, world anchoring, range/sky filtering and build references. Runtime checks cover game-time animation, distance fade, source disabling and camera counts. Play Mode is entered only by the final test runner.

`AeroWaterLightPreview.Capture()` captures the current building kit and workshop in a disposable Edit Mode review scene, plus the actual Tower lake using its main camera. It restores the camera, feature state and preview clock. `Capture(true)` also samples 60 motion frames; these are an Edit Mode animation preview, not gameplay footage. Preview output is in `Previews/`.

Render Graph review: material and immutable pattern/environment assets are explicitly assigned; Blitter binds the tracked depth input as `_BlitTexture`. The depth resource is read-only and the existing color attachment is declared read/write because blending preserves its contents. No new descriptor or texture is created, no graph resource is published globally, every pass-data field is assigned per recording, and the render callback is static. A native raster pass is intentional here: the generic material blit helper declares its destination write-only, whereas this overlay needs the destination loaded for blending. Only the required depth input is requested. Missing shader/pattern inputs skip the effect; a missing environment uses a black cubemap.

The effect has a bounded rendering budget; an end-to-end FPS improvement is not claimed without a representative player-build profile.

The earlier `Nearby-Motion.gif` and `Validation.json` document the superseded camera-wide projection. Current dry-kit captures intentionally show no caustic change; the lake and native-lamp captures demonstrate localized effects. Current repair verification is recorded in `LightingRepairValidation.json`.
