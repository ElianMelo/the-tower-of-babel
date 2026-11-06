# Volumetric clouds

Added 2026-11-05. Experimental until the user accepts it. A ray-marched cloud layer replaces the clouds painted into the old skybox. It follows the day/night cycle, rolls a new clear-day sky every few minutes and at sunrise, and turns into a low, dark deck while it rains. Every look is a ScriptableObject preset meant for hand tuning.

## Research

Four repositories were studied (cloned read-only into a scratch folder, nothing vendored):

| Repository | Licence | What was taken |
| --- | --- | --- |
| [jiaozi158/UnityVolumetricCloudsURP](https://github.com/jiaozi158/UnityVolumetricCloudsURP) (HDRP port to URP) | MIT | Lighting model: energy-conserving integration, dual-lobe Henyey-Greenstein with multi-scattering octaves, powder term, empty-space skipping, curved planet layer, reduced resolution plus upscale. |
| [SebLague/Clouds](https://github.com/SebLague/Clouds) | MIT | Density: height gradient × Perlin-Worley shape, detail noise eroding edges more than cores, blue-noise ray offsets, phase-based brightening toward the sun. |
| [Kirme/volumetric-clouds](https://github.com/Kirme/volumetric-clouds) (KTH thesis) | GPL-3.0 | Ideas only, no code: trace fewer pixels and interpolate the rest. |
| [miminashca/Volumetric-Clouds](https://github.com/miminashca/Volumetric-Clouds) (URP Render Graph) | none stated | Ideas only, no code: render-graph feature structure, bottom/top fade controls. |

The implementation is independent. GPL-3.0 and unlicensed code was not copied.

## How it renders

`AeroVolumetricCloudsRendererFeature` (on `Assets/Settings/PC_Renderer.asset`) draws after the skybox and before transparents, so rain drops, water and glass still render over the sky. When the camera is inside or above the cloud base (the top tower floors), rays that look down are composited after transparents instead. Otherwise the ocean would paint over the cloud below the player.

1. **Trace** at ½ resolution (`AeroCloudQuality.downsample`). Rays enter a cloud shell around a 1200 km planet, so the deck curves down to the horizon. Each texel traces to the farthest depth under it, so sky next to geometry keeps its clouds. Each step samples:
   - a 2D coverage map (where clouds form, equalized so coverage 0.4 marks ~40% of the sky as cloud regions; inside a region local coverage rises quickly, so the shape noise only breaks up edges)
   - a 128³ Perlin-Worley shape noise
   - a 32³ Worley detail noise that makes bases wispy and tops billowy
   - per-cloud height: the coverage map's height channel, sampled in ~1 km patches, lifts each cloud by up to the look's `heightVariation` (squared, so most clouds stay near the base and fewer float high). The traced shell reaches base + variation + thickness.

   Six light samples toward the sun or moon give self-shadowing. Distant (long) steps read coarser mips so the horizon does not sparkle. Steps start short (8 m) near the camera and grow by a quarter of the distance. Inside a cloud, the fog around the player is sampled finely. The first ~45 m around the camera thin out, so blocks you are building stay readable inside a cloud.
2. **Temporal**: per-camera history, reprojected at the cloud layer through URP's own projection, variance-clamped, 90% history.
3. **Composite** at full resolution (`Blend One SrcAlpha`; the alpha channel is left unchanged). Geometry nearer than the cloud layer stays unclouded, with a 150 m soft edge.

Distant clouds take on the fog colour (`hazeDistance`/`hazeStrength`) and fade out by `fadeDistance`. Overcast skies (coverage > 0.55) keep a grey haze down to the horizon where the deck lies beyond the fade distance.

Noise is baked by `AeroCloudNoise` (tiling Perlin, Worley, histogram equalization) into `Environment/Clouds/Noise/`, about 2.2 MB in total.

## Sky

The old `Epic_BlueSunset` skybox has photographed clouds in it, which doubled with the volumetric layer and never changed with rain. The Tower scene now uses `Environment/Clouds/AeroClearSky.mat` (`Skybox/Procedural`: Frutiger blue tint, soft sun disk). Its sunrise and sunset colours follow the sun. The day/night cycle still drives its exposure. To go back, set *Lighting → Environment → Skybox Material* to `Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat`.

## Day/night and weather wiring

`AeroVolumetricClouds` sits on *Aero island atmosphere* in `Scenes/Tower.unity`, next to `AeroDayNightCycle`, `AeroWetnessController` and `AeroRainController`.

- **Sun and moon.** By day the clouds are lit by the sun's colour at full (pre-overcast) intensity, since they sit above the rain dimming. At night the moon lights them, boosted by `moonLightBoost` so they read against the night sky. Sky light comes from the cycle's ambient sky/equator colours, boosted at night by `nightAmbientBoost`.
- **Ground shade.** Each preset's `sunOcclusion` × coverage sets `AeroDayNightCycle.cloudShade`, which dims only the sun on the ground (a cloudy day is slightly dimmer; heavy rain is much dimmer).
- **Rain.** When rain starts, a rain preset is picked. As `AeroWetnessController.RainAmount` fades in, the sky blends toward it through the library's `rainBlend` curve (85% at half rain). When the rain ends, a fresh clear sky is rolled and blends in.
- **Clear days.** A new sky is rolled every 240–480 game seconds and at every sunrise. Each blends in over 90 s. A roll is a weighted clear-day preset (cloud style), plus an amount tier (small/medium/large: sets coverage, scales size), plus a density tier (soft/medium/dense: scales density, shifts erosion, softness and sky light). Each value is picked inside the tier's range.
- **Networking.** Clouds are visual and local to each client, like the rain particles. Rain and time of day stay synced by `NetworkWeatherService`, so every player sees clear skies or rain at the same time, but individual cloud shapes differ between clients.

## Assets (all in `Assets/_Project/Environment/Clouds/`)

| Asset | Purpose |
| --- | --- |
| `AeroCloudWeather.asset` (`AeroCloudWeatherLibrary`) | Clear-day and rain preset lists with weights, tier lists, re-roll timing, rain blend curve, night boosts, planet radius |
| `Presets/*.asset` (`AeroCloudPreset`) | One complete look each (`AeroCloudLook`): amount/shape, motion, lighting, distance |
| `Tiers/Amount *.asset`, `Tiers/Density *.asset` | Random variation ranges for clear days |
| `AeroCloudQuality.asset` | Resolution, ray steps, light steps, max step, temporal blend |
| `Noise/*.asset` | Baked tiling noise |
| `AeroClearSky.mat` | Clean procedural sky behind the clouds |

Shipped presets (altitudes and densities: see the feedback round below):

| Preset | Weather | Look |
| --- | --- | --- |
| Clear Fair Weather Cumulus | Clear | Classic flat-based, domed cumulus |
| Clear Cotton Puffs | Clear | Small crisp popcorn cumulus |
| Clear Towering Cumulus | Clear | Tall congestus towers, deep shading |
| Clear Altocumulus Fleece | Clear | High thin sheet of small puffs |
| Clear Stratocumulus Rolls | Clear | Broad low rolls with blue gaps |
| Clear Frutiger Dream | Clear | Glossy airy clouds with saturated blue shadows |
| Clear Thin High Veil | Clear | Faint high veil |
| Clear Scattered Wisps | Clear | Sparse torn wisps |
| Clear Big Sky Banks | Clear | Large banks with wide open sky |
| Clear Golden Afternoon | Clear | Warm-lit with lavender shadows |
| Rain Heavy Nimbostratus | Rain | Low, thick, dark grey ceiling (most likely) |
| Rain Thunderstorm Anvil | Storm | Towering cumulonimbus, near-black bases |
| Rain Dark Squall | Storm | Fast, ragged dark squall line |
| Rain Grey Drizzle Overcast | Overcast | Even grey overcast |

## Tuning

- **Live:** in Play Mode, drag a preset into *AeroVolumetricClouds → Preview Preset* and edit the preset asset; changes show immediately. Clear the field to return to the weather. `Reseed(int)` and `RerollClearSky()` roll a new sky.
- **Contact sheet:** **Tower of Babel/Environment/Clouds/Capture preset contact sheet** renders every preset at morning, noon, sunset and night to `_Screenshots-and-previews-to-delete/Clouds/AllPresets.png`. `AeroCloudReview.Capture` takes chosen presets, times and camera.
- **New presets:** *Create → Tower of Babel → Clouds → Cloud preset*, then add the asset to a list in `AeroCloudWeather.asset`.
- **Authoring:** **Tower of Babel/Environment/Clouds/Author volumetric clouds** (idempotent) creates any missing assets, adds the renderer feature and wires the scene. It never overwrites edited presets. *Reset cloud presets to shipped values* and *Rebake cloud noise* restore defaults.
- **Measured cost** (2026-11-05, `FrameRateBenchmarkPlayModeTests`, Editor at 1920×1080, outside at ground level 160 m from the tower):
  - The *Aero cloud trace* pass takes 0.21 ms of GPU time.
  - The temporal and composite passes are too small to make the profiler's hotspot list.
  - Frame medians stay within Editor noise of the 2026-11-03 run (24.1 ms profiled, was 23.5 ms). See `Documentation/Performance/FrameRateBenchmark.json`.
- **Cost:** lower `AeroCloudQuality.primarySteps` / `lightSteps`, or set `downsample` to 3–4. Scene-view and game cameras are supported; orthographic and preview cameras are skipped.

## Feedback rounds (2026-11-05 and 2026-11-06)

The tower stands at y = 0 and its top floor is near 192 m (32 floors × 6 m). The island's hills reach about 56 m.

| Feedback | Change |
| --- | --- |
| Clouds too high in the sky (round 1) | Bases lowered to 380–750 m. Not low enough; superseded below. |
| Still too high; the latest tower floors should reach into the clouds (round 2) | Clear-day cumulus bases are now 120–170 m, so floors 20–32 sit in or just under the layer. Rain decks start at 100–115 m, below every clear sky. High layers stay above: fleece at 450 m, veil at 900 m. The default look is 150 m base, 450 m thick. |
| Cloud bases too flat | The underside ramps up over 50–90 m (at most 30% of the layer) with a rounded curve. It starts up to ~55 m higher per column (from the coverage map and local coverage), so each cloud's belly sits at its own height. Round 1 measured this ramp as a fraction of the layer, which pushed visible bases ~100 m above the preset altitude; it is now in metres. |
| Clouds too filled; should be softer and more transparent | Global extinction is 0.009 (was 0.014). Border gain is 2.2→1 (was 4→1.2), so fringes thin out gradually. Defaults are now density 0.8, erosion 0.6 and edge softness 0.4. Density tiers are lower: soft ×0.35–0.55, medium ×0.65–0.85, dense ×0.95–1.2. Rain presets got denser (2–3) so their decks stay dark. |
| No visible volumetric clouds (round 3, 2026-11-06) | The softening above left clear days nearly empty: seen top-down, Fair Weather covered 8% of the sky at coverage 0.38, Big Sky Banks 1%, Scattered Wisps and Thin High Veil 0%. The coverage map and the shape noise each thresholded the area, then detail erosion ate the weak cores. Now the map alone decides where clouds form, and inside a region local coverage starts at 0.35 and rises over 0.08–0.3 of the map (by edge softness). Density is remapped above the coverage threshold, so every cloud reaches full density in its core. Transparency still comes from density and extinction. Medium-tier clear skies now cover 75–100% of their coverage value; the softest tier still shows clouds. Thin High Veil went from density 0.25 to 0.8 (erosion 0.85 to 0.7), and Scattered Wisps from density 0.55 to 0.8 (erosion 0.9 to 0.8), so both stay visible at the softest tier. |
| Smaller clouds at different heights (round 4, 2026-11-06) | Noise tiles on the renderer feature: shape 2600 → 1200 m, detail 300 → 200 m, coverage 18000 → 7000 m. Larger presets shrank: Fair Weather 1 → 0.75, Frutiger Dream 1.2 → 0.9, Golden Afternoon 1 → 0.8, Towering 1.4 → 1.1, Stratocumulus 1.6 → 1.3, Big Sky Banks 2.2 → 1.8. New look value `heightVariation` (metres; default 200): clear presets 120–350 m (veil 350, wisps 280, fleece and towering 250, cumulus 180–220, stratocumulus 120); rain decks 40–120 m so they stay an even, low ceiling. Seen top-down, Fair Weather clouds went from ~1.5 km blobs to a few hundred metres. |

## Limits and follow-ups

- Inside a rain deck the top floors turn very dark and foggy. That is intended, but if building there is too hard, raise the rain presets' `baseAltitude` or lower their `density`.
- Seen exactly edge-on from a floor at cloud-base height, bases still read as a line. That is how a base looks at eye level.
- Wind offsets start at zero, so every session starts with the same coverage pattern over the island: open to the southwest, clouds to the north and east. Wind moves it about 1 km a minute at most.

- No cloud shadows on the ground (only the global `cloudShade` dimming). HDRP-style shadows would need a cookie on the sun.
- Clouds do not appear in water reflections; the reflection probe still sees the plain sky.
- Only PC_Renderer has the feature; the mobile renderer shows the plain procedural sky.
- Within one frame of review captures the jitter is constant, so thin regions look dithered in contact sheets. In game the temporal pass smooths it.

## Tests

`Tests/Rendering/EditMode/AeroVolumetricCloudsEditModeTests.cs` covers:

- look blending and clamping
- shipped presets (sparse clear days; dense, dark, low rain decks)
- tier ordering
- seeded clear-day rolls across amount and density tiers
- weighted picks
- rain blending and the clear sky that follows
- timer and sunrise re-rolls
- sun/moon lighting and ground shade
- preview override and wind drift
- noise tiling and coverage equalization
- renderer and scene wiring
- a GPU render: clouds above the horizon only, hidden behind near geometry, and the target's alpha untouched
- preset altitudes: top floors inside the layer, bases above the hills, soft density limits
- a GPU render from just above a layer's top: looking up shows nothing for a flat layer, and with height variation some clouds float overhead while others stay below (fails with the per-cloud offset switched off)
- shipped values: clear presets vary heights by at least 100 m, rain by at most 150 m, small noise tiles, median clear-day size at most 1
- a GPU render top-down over nine spots per clear preset at the softest density tier: each sky shows at least 30% of its coverage value as cloud (15% for thin high sheets) and stays under coverage + 0.2 (fails on the round-2 shader, where Fair Weather showed 9%)
- a GPU render from inside a cloud: a block 12 m away stays readable while a far wall fogs over
- a GPU render looking down from inside a cloud at transparent water: the fog lies over the water (the late composite)
