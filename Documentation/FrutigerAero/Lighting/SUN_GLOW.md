# Soft golden sun glow

**2026-11-14 — awaiting user review.** Reference: [`Frutiger Reference/Asset/Golden-Sun-Glow-User.webp`](../../../Frutiger%20Reference/Asset/Golden-Sun-Glow-User.webp), a hazy golden-hour field.

In the reference, the sun is not a point. It is a white-hot inner circle that smooths out into a warm glow, then a wide, faint golden halo, and the sky on the sun's side is washed gold. Distant hills and trees toward the sun lose contrast and melt into the halo. Away from the sun, the scene stays clear.

Before this change the sky was Unity's `Skybox/Procedural` sun disk (`_SunSize .035`). It has a single lobe whose size and softness are tied together, so it read as a point. Nothing else in the scene reacted to where the sun was.

**Second pass, same day (user feedback):**
- The sun covers a wider area, and its centre melts into the sky instead of reading as a disc.
- The sun's colour spreads across the whole skybox.
- God rays now also appear outdoors whenever you look toward the sun, not only inside the tower.

**Third pass, same day (user feedback):** "the sun is too much and draws too much attention".
- The sun is smaller again.
- The effect is now strongest when the centre of the view is near the sun, and smaller when the sun is at the side of the view.
- The outdoor god rays became world-space light shafts: a few separate beams in the air, not one big screen-wide beam.

**Fourth pass, same day (user feedback):**
- The sun now blends between two looks by where it is on screen:
  - **Focused** (the bigger second-pass sun) when you look straight at it.
  - **Edge** (the calm third-pass sun) at the screen's edge.
  - A blend in between.
- The outdoor light shafts are bigger and more visible.

## The glow model

One model drives everything. It lives in `Scripts/Rendering/AeroSunGlow.cs` (with a CPU copy used by the tests) and `Shaders/Aero/AeroSunGlow.hlsl`. θ is the angle from the centre of the sun, in degrees.

| Term | Shape | Role |
|---|---|---|
| Inner circle | `1 - smoothstep(r0(1-s), r0(1+s), θ)` | A wide, very soft edge (s ≈ .8), so it is almost a bell. Its HDR peak (3) stays close to the glow's, so it blooms without reading as a disc. Its colour is moved 45% toward white. |
| Glow | `exp(-(θ/r1)²)` | A bright warm glow around the circle. |
| Halo | `(1 + (θ/r2)²)^-1.5` | A wide tail, kept below the bloom threshold so it stays soft. |
| Horizon band | `side³ · exp(-height / washHeight)` | Warms the low sky on the sun's side. Low sun only. |
| Sky spread | `((1 + cos θ) / 2)^2.5` | The sun's colour washed over the whole sun-side sky (additive). |
| Sky tint | `sky → sun hue, brightness kept` | The whole skybox moves toward the sun's colour: 35% opposite the sun, 100% facing it, times the tint amount. A golden sun warms the whole sky; noon stays blue. |
| Sky veil | `sky × (1 - veil · saturate(glow + halo))` | Near a hazy low sun the blue gives way to the glow, so it reads golden instead of lavender. |

**Sun height.** Two sets of values, **low sun** and **high sun**, blend by the sine of the sun's height over `lowSunRange` (.08 to .8). This range is wider than the sun-colour curve's, because the 16:30 golden-hour preset has the sun about 22° up. The colour curve already treats that as fully "high", so it would have looked like midday.

**Starting values** (`AeroSunGlowSettings.Sun()`, saved in `Environment/Lighting/AeroDayNight.asset`). The *edge* look applies with the sun at the screen's edge (`highSun`, `lowSun`). The *focused* look applies looked at straight on (`highSunFocused`, `lowSunFocused`):

| | Circle r0 | Soft edge s | Glow r1 | Halo r2 | Circle | Glow | Halo | Band | Veil | Spread | Tint | Rays |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Low sun, focused | 2.6° | .85 | 11° | 36° | 3.5 | 2.4 | .9 | .55 | .8 | .35 | .45 | 1.4 |
| High sun, focused | 1.1° | .8 | 5° | 16° | 4 | 1.2 | .3 | 0 | .15 | .04 | 0 | .6 |
| Low sun, edge | 1.6° | .8 | 6.5° | 22° | 3 | 1.4 | .55 | .4 | .6 | .18 | .32 | 1.4 |
| High sun, edge | .9° | .75 | 3.5° | 11° | 3 | .8 | .2 | 0 | .1 | .03 | 0 | .6 |

**Looking at the sun.**
- **Two looks per frame.** The cycle evaluates both every frame: `SunGlow` (focused) and `SunGlowEdge`.
- **Per camera.** Before each game or Scene View camera renders (`AeroSunGlow.TrackCameras`), the two looks are blended by the sun's position on that camera's screen (`AeroSunGlow.ScreenFocus`):
  - fully focused within 10% of the screen's half-size from the centre
  - fully edge at the screen's edge, beyond it, and behind the camera
  - a smooth blend between
- **Other cameras** (reflections, probes) keep the last game camera's look, so a reflection cubemap's faces all match.
- **Reviews and tests.** `AeroSunGlow.Push(state)` pushes one look for every camera.

**Modifiers:**
- **Preset haze.** The halo, band, spread and veil grow with the atmosphere preset's `fogDensityScale`, at a response of .6. Misty mornings (1.4) and the golden afternoon (1.2) glow more, and weather and season fog changes carry through.
- **Overcast** (rain clouds). The inner circle goes. The glow halves and widens 1.6×. The halo drops to 60% and widens 1.4×. Rays drop to 40%.
- **Moon.** At night the glow follows the moon with `AeroSunGlowSettings.Moon()`: a small, cool glow with no ground haze and faint rays.
- **Dusk.** The sun's glow fades out as daylight falls from .2 to .1. The moon's fades in from .1 to 0. Nothing pops at the switch.

`AeroDayNightCycle.ApplyTime` evaluates the model and pushes it as shader globals: `_AeroSunGlowDir`, `_AeroSunGlowColor`, `_AeroSunGlowShape`, `_AeroSunGlowStrength`, `_AeroSunGlowTint`, `_AeroSunGlowSpread`, `_AeroSunHaze`, `_AeroSunHazeBase` and `_AeroSunGlowVeil`. Each tracked camera then re-pushes its blended look. Because they are globals rather than values on the runtime sky copy, Edit Mode captures show the glow too.

## Where it is drawn

- **Sky.** `Shaders/AeroSunGlowSky.shader` ("Tower of Babel/Sky/Sun Glow Sky") re-implements Procedural's Rayleigh/Mie atmosphere, ported from Unity's MIT-licensed built-in shader source. It keeps the same constants and property names, so `AeroClearSky.mat` keeps its Frutiger blue and the cycle's `_Exposure` write.
  - The sun disk is replaced by the glow, evaluated per pixel.
  - The whole sky is then tinted, veiled and lit by it.
  - The material was switched in place, so its GUID is unchanged.
  - The glow also shows in water reflections. Thick volumetric clouds cover the inner circle.
- **Haze over distant ground.** `AeroSunHazeRendererFeature` plus `Shaders/AeroSunHaze.shader`: one full-screen draw before post-processing, on the PC renderer only.
  - **What it does.** Far geometry toward the sun fades into the glow seen in that direction: `t = (1 - e^(-distance × .003)) × height thinning × amount`. Away from the sun nothing changes, and URP's flat fog is untouched.
  - **Open sea.** Below the horizon, pixels with no depth are hazed as sea level at `hazeBaseHeight` (0), because the transparent sea writes none.
  - **Clouds.** It fades when clouds cover the sun (`1 - cloudShade`).
  - **Which cameras.** It runs only for game and Scene View cameras that draw the skybox.
  - **Setting.** It can be turned off with the new **Sun haze** graphics setting, which is off on Low.
- **Outdoor light shafts.** The existing `AeroSunShaftRendererFeature` (the tower's interior shafts) now also draws world-space light shafts in the open air with `Shaders/AeroSunRays.shader`. They replaced the screen-space god rays of the second pass, which read as one huge beam over the whole screen.
  - **The march.** Each pixel marches its view ray through world space, up to 110 m from the camera, 24 steps at half resolution (12 at quarter on Low).
  - **The beams.** A drifting noise stretched along the sunlight makes broad beams with gaps between them (fourth pass: bigger and brighter):
    - gaps: 60% of the air
    - width: about 8 m
    - length: about 120 m along the light
    - drift: 0.35 m/s
  - **Shadows.** The sun's shadow map (tower, trees, hills) cuts the beams.
  - **Height.** They thin above the ground (2% per metre).
  - **Light.** Forward scattering (`.05 + ((1 + cos)/2)^6`) makes them strong looking toward the sun and faint from the side. Parallel beams converge toward the sun in perspective, like real crepuscular rays.
  - **Composite.** The depth-aware upsample is the interior shafts' scheme: a 4×4 ordered dither resolved by a sliding four-texel box.
  - **Strength.** It drops to zero with the sun more than about 110° behind the view, and by 85% at full tower interior weight, where the shadow-mapped shafts carry the light.
  - **When it runs.** Only for game and Scene View cameras that draw the skybox.
  - **Setting.** It follows the **Sun shafts** graphics setting: Off disables it, Low uses quarter resolution and half the steps.
  - **Implementation note.** The scatter and the composite have separate materials. Values set on a shared material apply when the recorded frame runs, so both steps would otherwise see the last step's values.
- **Clouds.** The far haze and overcast haze wall in `Aero/AeroClouds.hlsl` add the glow (without the inner circle) to the fog colour. Distant clouds near the sun melt into gold instead of showing grey patches inside the halo.
- **Flare.**
  - The `AeroSunFlare.asset` halo element drops from .32 to .1, because the sky now draws the halo.
  - The core and halo take the light's colour, so they warm at sunset.
  - The flare fades when volumetric or rain clouds cover the sun.
- **Bloom.** The atmosphere director now sets bloom scatter: .68 at high sun (the accepted Restrained value), up to .8 at low sun, so the glow spills over silhouettes. The threshold rules are unchanged.

Apply or re-apply everything with **Tower of Babel → Environment → Apply soft sun glow** (`Editor/AeroSunGlowAuthoring.cs`). It writes the tuned settings into the profile and wires the haze and rays shaders on the PC renderer.

## Review

**Tower of Babel → Rendering → Capture soft sun review** (`Editor/AeroSunGlowReview.cs`) turns the camera toward the sun or moon at 07:41, 12:00, 16:30, 17:46 and 00:29. It renders clear, cloudy and rain rows into `_Screenshots-and-previews-to-delete/SunGlow/`:

- `SunGlow4.png`: from the default review viewpoint over the sea.
- `Before.png`: the same views with the old Procedural sun.
- `SunGlow3Ground.png`: from the field near the main camera, in autumn, at 07:41, 16:30, 17:17 and 17:38.
- `SunGlow3RaysOnly.png`: the same views with the light shafts alone. To produce it, set `isolateRays` on the PC renderer's sun shaft feature.
- `SunFocusBlend.png`: the same field at 07:41 and 16:30, with the camera turned 0°, 18° and 34° away from the sun (`yawFromSun`). It shows the focused look giving way to the edge look.

## Tests

- `AeroSunGlowEditModeTests` covers:
  - **The CPU model:**
    - brightest at the centre, steady fall-off, no hard edge
    - a peak close to the glow
    - wide, golden and tinted at low sun; clean at noon
    - the colour spreading to the far side of the sky
    - overcast, haze and the moon
  - **The saved profile.**
  - **The cycle:** afternoon vs noon, moon at night, no pop at dusk, clouds, no allocation.
  - **Director bloom scatter.**
  - **Shader compilation and asset wiring.**
  - **Focus:**
    - The focused look is bigger and warmer than the edge one, and a half blend lies between them.
    - The sun at the screen's centre gets the full look, part way out a blend, and at the edge or behind the camera the calm look.
  - **Light shaft strength:** facing the sun, beside it, behind the camera, hidden.
  - **Three GPU checks:**
    - The sky matches the model, and away from the sun keeps its brightness while taking on its colour.
    - Light shafts show toward the sun, are faint from the side, and are separate beams covering only part of the view.
    - The haze veils far ground toward the sun only.
- `AeroIslandAtmosphereEditModeTests` checks the four flare elements.
- `GameSettingsEditModeTests` and `SettingsMenuPlayModeTests` check the Sun haze setting.
- The interior shaft tests (`AeroAtmosphereEditModeTests`) still pass with the outdoor rays added to the same feature.

## Not included

- Backlit terrain grass, which would mean translucency in the third-party `FullOpaqueGrass` shader graph.
- Sun-side rim light on characters.
- **GPU cost not measured for the light shafts.** The haze pass is one full-resolution draw. The light shafts are a 24-step march at half resolution plus one full-resolution composite.
  - **Rough numbers.** `TowerRayRuntimeReviewTests` measured its shafts-off baseline camera at 1920×1080, in GPU time:
    - 1.80 ms before these changes
    - 2.10 ms with the second pass's screen-space rays
    - 1.86 ms with the world-space shafts (2026-11-14)

    That fixture is a tower view, so it says little about open-air shafts. These are single noisy runs, so its report was not updated.
  - **For a proper figure,** use `FrameRateBenchmarkPlayModeTests`.
- **Tower occlusion not seen in Edit Mode.** The tower isn't built in Edit Mode, so the review captures only show hills and clouds cutting the rays. The tower's silhouette against the sun should be checked in Play Mode.
