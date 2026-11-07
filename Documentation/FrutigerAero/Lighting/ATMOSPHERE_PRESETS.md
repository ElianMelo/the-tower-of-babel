# Atmosphere presets: time of day × weather × season

Added 2026-11-07. The user asked for presets for five times of day, three weathers and four seasons, with every permutation varying the global volume, fog, lighting, floating particles and colour temperature. The user also rejected **Bright Aero** and **Y2K** and asked for looks closer to **Restrained** and the Frutiger Aero references: saturated, but calm and cosy rather than aggressive.

**Status:** awaiting the user's review in the F4 graphics window.

## How it is built

There are 5 × 3 × 4 = **60 permutations**. They are composed from layers instead of 60 hand-made assets, so each permutation stays consistent with the others and is still tunable on its own.

| Layer | Kind | What it sets |
| --- | --- | --- |
| Time of day (5) | Full look | Exposure, contrast, saturation, white balance, colour filter, bloom, vignette, split toning, sun tint and strength, ambient tint and strength, shadow strength, sky exposure, fog colour and density, particle brightness, day/night particle switch |
| Weather (3) | Adjustment | Offsets for grade and temperature, multipliers for sun, ambient, shadows, bloom and fog, fog desaturation, particle amount |
| Season (4) | Adjustment and particles | Warm or cool shift, colour filter, fog tint and density, sun and ambient tint, day particles and night particles |
| Permutation override | Optional adjustment | A fix for one exact time × weather × season combination, found during review |

- **Assets.** All values live in `Assets/_Project/Environment/Lighting/AeroAtmospherePresets.asset` (`AeroAtmospherePresetLibrary`). Edit it in the Inspector. To start a review fix, add an entry to **Overrides**.
- **Runtime.** `AeroAtmosphereDirector`, on the *Aero island atmosphere* object in the Tower scene, runs every frame. It does four things:
  - It reads the synced clock and blends between the two nearest time presets with a smoothstep.
  - It weights the weather. Rain comes from the wetness rain amount. Cloudy comes from volumetric cloud coverage between 0.40 and 0.65. Clear is whatever remains. A weather forced from the window takes over, and weights ease over 4 s.
  - It crossfades season changes over 3 s.
  - It applies the result to a runtime global volume. That volume has priority 1: above the Restrained base volume (0) and below the tower exit volume (5).
- **Lighting.** The day/night cycle takes the lighting part through `AeroDayNightCycle.grade`. That covers sun tint and intensity, shadow strength, ambient, fog colour and density, and skybox exposure.
- **Particles.** Seasonal particles come from `AeroAmbientParticles`. It runs four camera-following particle systems: dot, petal, leaf and flake. The shapes are drawn procedurally in `Shaders/AeroAmbientParticles.shader`, which is soft, depth-faded and alpha-blended. Glowing particles use HDR colour, so the existing bloom carries them. Under a built roof, falling shapes drop to 10%.
- **Restrained stays the base.** The accepted `AeroAtmosphere.asset` profile is unchanged under the grade. That keeps neutral tonemapping, the lift/gain and the lens-flare settings.
- **Unchanged systems.** The existing rain overcast (`AeroRainController`) and cloud shading still apply on top. The weather layers are kept mild so effects are not counted twice.

## Research behind the values

| Topic | Finding | Applied as |
| --- | --- | --- |
| Time of day | Golden hour is about low-angle warm light and long soft shadows. Blue hour after sunset is mostly blue. | Morning: peach sun, cool shade, mist. Afternoon: warm sun, teal shadows, honey haze, softer shadows. Night and Midnight: blue |
| Night readability | Real night is almost black and unplayable. Games use a dim, slightly desaturated blue moonlight (the Purkinje shift) and a low blue fill, and warm local lights guide the eye. | Night is lifted (+0.12 EV) with ambient fill. Midnight is deeper but never black, and its fog stays blue. Lamps and fireflies carry the bloom. |
| Overcast | Overcast light scatters in all directions and removes strong light and shadows. | Cloudy: shadows ×0.6, contrast −2, pearly fog |
| Rain | Fog and weather swallow distance and lower saturation, while lamps become points of warmth. Wet surfaces get darker and glossier. | Rain: saturation −5, cooler, shadows ×0.4, mist ×1.3, bloom ×1.1 for wet highlights. The wetness film stays on its own system. |
| Seasons | Common game approach: summer warm and saturated, winter blue and desaturated. Spring and winter lean cool, summer and autumn warm. Spring and summer must not look the same. | Spring: pastel green and petals. Summer: most vivid, golden motes, fireflies at night. Autumn: amber, hazier, falling leaves. Winter: cool, brighter, −5 saturation, snow. |
| Particles | Seasonal ambient mods pair falling leaves with pollen and forest haze, and petals or snow with wind drift, wobble and rotation. | Leaves and petals tumble and drift on noise. Snow falls more slowly. Rain clears floating particles but thickens snow, which counts as precipitation. |
| Frutiger Aero | Built on a white, sky-blue and grass-green palette with optimistic nature imagery. Saturated but light and open, with no harsh grading. | Saturation stays between 0 and 26 (the rejected Y2K used 30). Contrast stays between 0 and 12. Bloom threshold is at least 0.88, so marble stays readable. No grain and no chromatic aberration. |

Sources:
- [fxguide on game rain](https://www.fxguide.com/?p=42356)
- [Artist-directable real-time rain (2006)](https://advances.realtimerendering.com/s2006/Chapter3-Artist-Directable_Real-Time_Rain_Rendering_in_City_Environments.pdf)
- [World of Level Design: overcast lighting](https://worldofleveldesign.com/categories/ue4/overcast-lighting-sky-atmosphere.php)
- [PremiumBeat: why moonlight is graded blue](https://www.premiumbeat.com/blog/why-color-grade-moonlight-blue/)
- [Night scene fill-light fix](https://bugnet.io/blog/how-to-fix-night-scene-too-dark-no-fill-light)
- [Creative Bloq: lighting a game environment](https://www.creativebloq.com/video-games/how-light-your-basic-games-environment-51514875)
- [Vintage Story seasonal colour grading](https://www.vintagestory.at/forums/topic/19132-seasonal-color-grading-mod)
- [80.lv: UE5 seasons lighting study](https://80.lv/articles/the-changing-seasons-ue5-feudal-japan-cinematic-lighting-studies)
- [Pixel Joint: seasonal palette thread](https://pixeljoint.com/forum/forum_posts.asp?TID=26766)
- [Ambient Leaves](https://modrinth.com/mod/ambient-leaves)
- [Seasonal Ambiance](https://moddex.gg/mod/seasonal-ambiance)
- [Frutiger Aero (Wikipedia)](https://en.wikipedia.org/wiki/Frutiger_Aero)
- [daisyUI Frutiger Aero trend](https://trends.daisyui.com/trend/frutiger-aero/)

The project references support the same direction:
- `Frutiger Reference/Asset/AeroLandscape.png` (clear Mid-day)
- `Forest-Haze-User.png` and `Sun-Shafts-Cloud-User.png` (restrained morning haze and shafts)
- `Aurora-Colours-User.png` (the faint violet in Night highlights)
- `UI/02-Chat-Reference.png` (sky blue, grass green, white)

## Default values

Values at each time of day before weather and season:

| Time | Clock | Exp | Con | Sat | Temp | Bloom | Sun | Ambient | Shadows | Fog | Fog × |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Morning | 07:00 | +0.05 | 4 | 14 | −3 | 0.60 | peach ×0.90 | ×1.00 | 0.75 | pale blue-peach | 1.4 |
| Mid-day | 12:00 | +0.05 | 7 | 16 | −7 | 0.55 | white ×1.05 | ×1.05 | 0.85 | sky blue | 1.0 |
| Afternoon | 16:30 | +0.03 | 6 | 17 | +5 | 0.65 | gold ×0.95 | warm ×1.00 | 0.75 | warm haze | 1.2 |
| Night | 21:00 | +0.12 | 5 | 11 | −10 | 0.75 | — | blue ×1.15 | 0.60 | navy | 1.1 |
| Midnight | 00:00 | −0.12 | 5 | 10 | −13 | 0.80 | — | blue ×0.85 | 0.50 | deep navy | 0.9 |

Weather and season adjustments:

| Layer | Exp | Con | Sat | Temp | Other |
| --- | --- | --- | --- | --- | --- |
| Clear | 0 | 0 | +1 | 0 | — |
| Cloudy | −0.03 | −2 | −3 | −2 | Shadows ×0.6, fog ×1.2 (20% greyer), bloom ×0.85, particles ×0.7 |
| Rain | −0.05 | −1 | −5 | −4 | Shadows ×0.4, fog ×1.3, bloom ×1.1, floating particles ×0.2, snow ×1.7 |
| Spring | +0.03 | −1 | +3 | −1 | Green filter, fog ×1.1. Day: pink petals. Night: cyan-green pollen glow. |
| Summer | +0.03 | +1 | +4 | +2 | Sun ×1.05, fog ×0.9. Day: golden motes. Night: yellow-green fireflies. |
| Autumn | 0 | +1 | +1 | +7 | Amber filter and sun, fog ×1.35. Day: orange leaves. Night: warm motes. |
| Winter | +0.06 | +1 | −5 | −8 | Cool filter, sun ×0.85, fog ×1.25. Snow by day and night, thicker under cloud and rain. |

Rebuilding the asset from these defaults overwrites any tuning. To do it, use **Tower of Babel → Rendering → Reset atmosphere presets to defaults**. **Apply atmosphere presets** only re-wires the scene and keeps the tuned values.

## Reviewing in the game (F4)

**F4** opens or closes the graphics window: the compact top-left panel that used to be reached only through F3 → Visual controls. **Escape** also closes it. Hold the right mouse button to look around while it is open.

| Row | Controls | Scope |
| --- | --- | --- |
| Time of day | Morning, Mid-day, Afternoon, Night, Midnight, Cycle, −1 h, +1 h, Freeze | Synced |
| Weather | Clear, Cloudy, Rain, Auto | Rain is synced. The cloudy look is local. The forced choice shows in brackets. |
| Season | Spring, Summer, Autumn, Winter | Local, remembered in `PlayerPrefs` (`Aero.Season`) |
| Preset | < Prev, Next >, Live | Steps through all 60 permutations (1/60 … 60/60) and freezes time for review. Live returns to automatic weather and a running clock. |
| Readout | — | Exposure, contrast, saturation, temperature and tint (with a warm, neutral or cool label), bloom, vignette, sun, ambient, shadows, fog density and colour, and the strongest particle layer |
| Clouds and wetness | Cloud preset preview, Auto, Reroll, Wet | Local |

The F3 **World** tab's **Post look** action is replaced by **Season → Cycle**.

## Review captures

Use **Tower of Babel → Rendering → Capture atmosphere preset contact sheet** in Edit Mode. It writes `_Screenshots-and-previews-to-delete/AtmospherePresets/AllPresets.png` and `AllPresets.txt`:
- **Layout.** The PNG has 12 rows (season × weather) by 5 columns (time). The text file lists every permutation's values.
- **What it can't show.** Particles only run in Play Mode, and the skybox exposure only changes in Play Mode, so the sheet shows neither.

First review notes (2026-11-07):
- Mid-day saturation was lowered (18 → 16, summer +5 → +4) because summer grass read neon.
- Midnight was made deeper than Night.
- Morning and rain fog were thinned so the land isn't washed out.

## Validation

EditMode:

| Class | Tests | What they cover |
| --- | --- | --- |
| `AeroAtmospherePresetEditModeTests` | 11 | All 60 permutations are distinct and round-trip. Every permutation stays inside the Restrained comfort bounds. Warm and cool ordering for time, weather and season, matching the research. Particles for each season, and how weather changes them. Smooth time blending with no jumps between minutes. Overrides are isolated. The shader compiles. Director grade and season persistence. Day/night grade application. Tower scene wiring. |
| `AeroPostProfileEditModeTests` | 5 | Restrained bounds. Bright Aero and Y2K are retired. Volume priorities. Marble does not clip under the brightest preset. |
| `CheatAndTerrainEditModeTests` | 11 | F4 toggle, graphics window controls and preset browser. |

PlayMode: `AeroAtmospherePresetPlayModeTests` checks the runtime grade, the summer motes, the switch to winter snow and cleanup.

Season is local to each player for now. Syncing it through the weather service is a possible follow-up.
