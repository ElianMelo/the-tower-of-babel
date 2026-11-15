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
   - form (added 2026-11-14; the defaults leave every other preset exactly as before):
     - `stretch` draws the shapes out along the wind, so streaks point the way the sky drifts. Cloud fields stretch by its square root, so they line up in bands.
     - `fibres` draws cirrus from a baked 1024² cirrus hair map (since 2026-11-15):
       - The map is traced hair. Strands follow a gently curving, wind-combed flow field. Each wisp is a few brush strokes of 10–25 thin hairs that start together, run side by side and fade out one by one into a feathered tail; some strokes hook at the head (mares' tails). Faint loose threads link the wisps, a soft glow surrounds them, and clear sky lies between.
       - Above 0.5 (separate strands), cirrus is a thin sheet halfway up the layer, textured with the map, and the volume fades out (at 1 the march is skipped). No shipped preset uses the sheet since 2026-11-15 (fourth round); looks without it skip its texture fetches. Two copies of the map (the second larger, turned 11° and drifting a little faster as the sky evolves) hide the tile's repeats. The weather map spreads the wisps over the sky with a threshold that is soft over kilometres. Opacity grows on slanted paths (capped at about 3×). Haze builds 2.5× faster than for the volume, so distant wisps melt into the horizon.
       - The sheet is sampled with screen derivatives and 8× anisotropic filtering, so strands toward the horizon stay sharp along their length.
       - Below 0.5 (veils), fibres only add a faint texture to the volume.
     - `raggedBase` is the share of each cloud, from its underside up, that is torn and wispy; above it, billows. It defaults to a third (the old fixed value).
     - `sculpt` lowers the local coverage and tapers it with height. The 3D noise then outlines tall clouds instead of the weather map.
     - `updraft` makes billows rise through the column (metres per second).

   Six light samples toward the sun or moon give self-shadowing. Distant (long) steps read coarser mips so the horizon does not sparkle. Steps start short (8 m) near the camera and grow by a quarter of the distance. Layers deeper than 800 m (base to highest top) get more steps: 2× at 1100 m and deeper (`AeroVolumetricCloudsRendererFeature.PrimarySteps`). Every clear preset except Towering Cumulus is shallower and keeps the authored count. The deep rain decks get the extra steps too. Inside a cloud, the fog around the player is sampled finely. The first ~45 m around the camera thin out, so blocks you are building stay readable inside a cloud.
2. **Temporal**: per-camera history, reprojected at the cloud layer through URP's own projection, variance-clamped, 90% history.
3. **Composite** at full resolution (`Blend One SrcAlpha`; the alpha channel is left unchanged). Geometry nearer than the cloud layer stays unclouded, with a 150 m soft edge.

Distant clouds take on the fog colour (`hazeDistance`/`hazeStrength`) and fade out by `fadeDistance`. Overcast skies (coverage > 0.55) keep a grey haze down to the horizon where the deck lies beyond the fade distance.

Noise is baked by `AeroCloudNoise` (tiling Perlin, Worley, histogram equalization, traced hair) into `Environment/Clouds/Noise/`, about 3.6 MB in memory. The 1024² R8 cirrus hair map adds about 1.4 MB of that (baked in about 1 s).

## Sky

The old `Epic_BlueSunset` skybox has photographed clouds in it, which doubled with the volumetric layer and never changed with rain. The Tower scene now uses `Environment/Clouds/AeroClearSky.mat`, with Frutiger blue tint. Its sunrise and sunset colours follow the sun. The day/night cycle still drives its exposure. Since 2026-11-14 it uses the soft sun shader (`AeroSunGlowSky`, the same atmosphere as `Skybox/Procedural`), which replaces the point sun with a glowing inner circle and halo; see [soft golden sun glow](SUN_GLOW.md). Distant cloud haze near the sun takes on the same glow. To go back, set *Lighting → Environment → Skybox Material* to `Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat`.

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
| Clear Towering Cumulus | Clear | Cumulus congestus: separate towers sculpted by turbulence, flat shared bases, cauliflower tops, rising billows |
| Clear Altocumulus Fleece | Clear | High thin sheet of small puffs |
| Clear Stratocumulus Rolls | Clear | Broad low rolls with blue gaps |
| Clear Frutiger Dream | Clear | Glossy airy clouds with saturated blue shadows |
| Clear Thin High Veil | Clear | Cirrostratus: a milky ice veil with faint fibres along the wind |
| Clear Scattered Wisps | Clear | Scattered cumulus built like Towering Cumulus: small sculpted towers with cauliflower tops, flat bases and wind-torn wispy fringes over open sky |
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
- **Cloud families:**
  - Ice clouds (cirrus, cirrostratus): thin high sheet, float under ~40 m, `stretch` 2–5, `fibres` (faint .1–.3 for veils, 1 for strands), `powder` 0. Strand cirrus (`fibres` 1) is a textured sheet: `cloudSize` scales the strokes, `density` and `thickness` set their opacity, `coverage` how much of the sky has wisps, and `hazeStrength` (about .75) how softly they fade into the horizon. Float, erosion, cloud type and shear do not shape it.
  - Tall convective towers: `sculpt` near 1, `raggedBase` under ~.1, `updraft` 1–3 m/s, a float under ~150 m (a large float shears tall towers). Scattered Wisps is the same build, smaller (size .4), shorter (700 m), more scattered (coverage .26), with more erosion and shear for wind-torn fringes. Keep float + thickness at 800 m or less to keep the authored ray steps.
  - Leave `stretch` 1, `fibres` 0, `sculpt` 0 and `raggedBase` ⅓ for ordinary cumulus.
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

## Feedback round (2026-11-14): closer to real clouds

Captures: `_Screenshots-and-previews-to-delete/Clouds/Rework/BeforeAfter_Horizon.png` and `BeforeAfter_Up.png`. Before is on the left, after on the right; rows are Wisps, Veil and Towering; columns are noon and sunset.

| Feedback | Cause | Change |
| --- | --- | --- |
| Thin High Veil does not look like clouds | A 350 m float bent the 250 m sheet into twisted ribbons. Erosion .7 with the torn-base rule left nets of lines. | Cirrostratus: one flat sheet (float 20 m) at 1000 m, very soft edges, erosion .2. Shapes are stretched 2.5× along the wind, with faint fibres (`fibres` .2). It looks milky and the sun shines through. |
| Scattered Wisps do not look like clouds | The same ribbons (280 m float, erosion .8), low at 170 m. | Cirrus at 950 m, float 40 m, strands along the wind (`fibres` 1). First version: strands from the 3D Worley detail noise. |
| Scattered Wisps still do not look like clouds (second round) | The Worley strands were short, blurry slashes clumped into patches (claw marks, smeared paint). The billowy shape noise cut each band into small blobs: a field of flecks toward the horizon. | Strands now come from the baked cirrus map: long thin filaments that curve, fork and feather out. Strand cirrus drops the billow noise, so bands are smooth, drawn out along the wind and bent by the flow field. Cutting bands with a quarter of the billow noise brought the flecks back and ended bands bluntly, so it was rejected. Look: density 1.2, size 1.3, `stretch` 3, edge softness .9. Captures: `_Screenshots-and-previews-to-delete/Clouds/Wisps/BeforeAfter_Wisps.png` (left before, right after; horizon above, overhead below). |
| Scattered Wisps still do not look like clouds (third round, 2026-11-15) | Still a 250 m deep volume: each strand stood as a wall that the view from below smeared into a broad band, and the weather map cut the bands off along smooth outlines (smeared paint, a ghost). The smooth noise contours never looked like hair. | Strand cirrus is now a thin sheet halfway up the layer (no volume; the march is skipped), textured with the new traced hair map: wisps of fine hairs with feathered tails, some hooked, with clear sky between them. More haze (`hazeStrength` .5 → .75, sheet haze 2.5× faster) lets the strokes toward the horizon fade instead of packing into white flecks. The old 512² cirrus map is replaced. Captures: `_Screenshots-and-previews-to-delete/Clouds/Wisps3/BeforeAfter_Wisps.png` (left before, right after; horizon above, overhead below). |
| Scattered Wisps "don't even look like cloud" (fourth round, 2026-11-15); Towering Cumulus is the best shape | No cirrus form (Worley strands, a strand map in a volume, a hair-textured sheet) read as cloud. | Scattered Wisps is rebuilt as scattered cumulus on the Towering Cumulus design: `sculpt` 1, `raggedBase` .1, `updraft` 2.5 m/s, cloud type .75, the towers' lighting. Smaller (size .4), shorter (700 m, float 100 m, so the authored ray steps), more scattered (coverage .26), with wind-torn fringes (erosion .85, shear 160), density 1.2, base 160 m. The cirrus sheet and hair map stay in the shader for cirrus looks (the veil's faint fibres use the map). Captures: `_Screenshots-and-previews-to-delete/Clouds/Wisps4/BeforeAfter_Wisps.png` (left the third round, right now; horizon above, overhead below) and `Final_Times.png` (wisps above, towers below; morning, noon, sunset). |
| Towering Cumulus: fragments and weird lines | 1. The weather map's outline rose up the whole 1.1 km tower as a flat wall.<br>2. The torn base reached a third of the cloud: 370 m of streaky fragments.<br>3. A 250 m float sheared the towers.<br>4. Rays toward the horizon ran out of steps inside distant towers, at a distance that changes row by row, which drew horizontal lines. More light samples, shorter steps and longer settling did not help; more ray steps did. | Cumulus congestus: `sculpt` 1 (turbulence outlines bulging towers that narrow toward cauliflower tops), `raggedBase` .06 (60 m), float 100 m, narrower towers (size .6, 1000 m tall), erosion .75, crisp edges, bluer shadows, `updraft` 2.5 m/s. Deep layers march 2× steps. |

Every other preset keeps its look: the new settings default to the old behaviour. The softest-tier coverage measurements for the untouched presets match the earlier values exactly.

The Towering Cumulus trace costs about 2× while that sky is shown; its deep layer gets 2× the steps. Scattered Wisps keeps the authored steps. The frame-rate benchmark has not been re-run.

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
- the 2026-11-14 rework:
  - the new form settings blend, clamp and default to the old look
  - veil, wisps and towers have the shipped values and match their definitions; the wisps are built like the towers but smaller, shorter, more scattered and more torn, within the authored ray steps
  - only the deep tower layer gets 2× steps
  - the form constants and rise offset reach the shader
  - billows rise at the updraft speed
- the cirrus sheet, on a test-only strand cirrus look (the wisps' third-round values):
  - a GPU render straight up: strands run across the screen in an east wind and up it in a north wind. Measured anisotropy (hair sheet, 2026-11-15): 0.32 east, 2.23 north, 0.93 without fibres.
  - a GPU render with a blank hair map: the sky stays empty (0.000), while a look that keeps the volume (`fibres` .5) still shows cloud (0.644): every wisp comes from the sheet, with no volume left to smear
  - a GPU render toward the horizon: mean cloud contrast in the 6° above the horizon over that at 22–38° is 0.19 shipped and 0.33 without haze (the busy band of flecks)
- the cirrus hair map: R8, deterministic, wisps with clear sky between them (8–40% of texels), combed along the wind, wraps across both tile edges, wired to the renderer as R8 with anisotropic filtering
- a GPU render of the wisps above the horizon: sculpting removes flat walls (column coherence 0.482 shipped, 0.516 unsculpted) and the scattered clouds leave more sky open than the towers (58% against 74% of the view)
- a GPU render of the towers above the horizon: brightness edges line up down columns (walls) and along rows (lines) less than without sculpting, with the old step count, and in the reported look. Columns: 0.455 shipped, 0.540 unsculpted, 0.494 reported. Rows: 0.332 shipped, 0.422 with the old step count, 0.501 reported.
