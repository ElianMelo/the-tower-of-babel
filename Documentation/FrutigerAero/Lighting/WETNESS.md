# Global wetness: dew and rain

Added 2026-10-29. This is experimental until the user accepts it. The film itself is visual only, but since 2026-10-31 whether it rains is decided by the server and synced to every player; see [rain and synced weather](RAIN.md).

**Dew off by default (2026-10-31).** Rain now ships ([RAIN.md](RAIN.md)) and wets surfaces without this switch. While `WetnessEnabled` is off, there is no dew, and `_AeroWetReady` stays 0, so shaders skip the wet path entirely. The F3 **Rain** toggle and **Wetness preset** still preview the film. Afterwards the surfaces dry completely, not back to dew. Turn it on with the controller's **Wetness Enabled** field (or `WetnessEnabled = true`) once rain ships.

## Driver

`Scripts/Rendering/AeroWetnessController.cs` sits on *Aero island atmosphere* in `Scenes/Tower.unity`, next to the day/night cycle and tower atmosphere. It publishes:

- `_AeroWetness`: film coverage, 0–1.
- `_AeroRain`: active drops, streaks and ripples, 0–1.
- `_AeroWetClock`.
- Drop and streak parameters.
- The tower shelter.
- The three DYNAMIC textures.

Tuning lives in `Environment/Lighting/AeroWetness.asset` (`AeroWetnessProfile`):

| Setting | Value |
| --- | --- |
| Dew | 0.10 by day, 0.25 at dawn, following the day/night time |
| Rain film | 1.0 |
| Ramp in | 6 s |
| Dry out | 10 s (was 40 s; shortened 2026-10-31) |
| Rain fade | 3 s |
| Drop tile | 0.45 m |
| Streaks | 0.9 × 1.6 m, falling at 0.55 |

Textures are the package originals, linear and repeating:
- `DYNAMIC- Falling Rain/Rain/Textures/rain_drops_01.tga`: RG normal, B per-drop phase, A resting drops.
- `rain_streaks_01.png`: RG normal, B mask.
- `DYNAMIC- RainWetness/Shaders/Decal Shaders/Puddles/rain_ripples.png`: Lagarde-packed ripples.

API:
- `Raining` toggles rain.
- `OverrideWetness(value | null)` forces a film.
- `CycleWetnessPreset()` steps Dry → Dew → Soaked → Weather. Changing `Raining` clears a forced preset, so turning rain off always dries the world.
- `Tick(dt)` advances the ramps for tests.
- `previewTime` fixes the shader clock.

The F3 developer menu's **World** section has:
- a **Rain** toggle (synced through `NetworkWeatherService`, see [rain](RAIN.md));
- **Wetness preset**;
- **Post look** (see [post profiles](POST_PROFILES.md)).

## Shading (`Shaders/Aero/AeroWetness.hlsl`)

`AeroApplyWetness(albedo, smoothness, normal, positionWS, geometricNormal, porosity, exposure)` works by porosity:

- **Porous materials** (stone, sand: porosity ≈ 0.85) darken and saturate.
- **Sealed materials** (polished marble 0.12, glass and metal 0) turn glossy.

The film flattens top faces. On top of that:
- resting drops appear with dew, and moving drops with rain;
- walls get triplanar streaks;
- decks get ripples.

Drops and ripples fade between 12 and 35 m so they never alias into sparkle. All rain detail sits behind a uniform branch, so materials compile only one extra local keyword.

**Rain shelter.** The tower is a stepped cone:
- the wall radius is `96 - 3 × (floor/2 + 1)` and the floor height is 6 m;
- a point is sheltered when the floor above extends past it;
- interiors receive no drops and keep only dew, while outer terraces and the top floor get rain;
- the parameters come from `AeroTowerAtmosphere`.

**Glass lids.** The river's glass lid also collects drops from above.

## Walkable floors (user feedback, 2026-10-29)

The first pass covered the marble decks with dew beads and rain drops. At eye height that read as grey speckle in dew and a sandpaper carpet in rain.

`AeroApplyWetnessBeads(..., beads)` now scales visible drops per material through `_WetBeads`:

| Surface | `_WetBeads` |
| --- | --- |
| Walkable decks (`Aero_WhiteMarble_Floor_Tile`, `Step_Tile`, `GlassWaterFloor`) | 0 |
| Arches and pillars | 0.35 |
| Props, glass and leaves | 1 (full beads) |

With beads off, a floor shows:
- **dew:** only a slightly glossier film;
- **rain:** a clean wet mirror with larger, gentler ripple rings.

`AeroFeedbackEditModeTests.WalkableFloors_StayCleanWhenDewyInsteadOfSpeckled` measures pixel detail on the deck. Dew must stay within 15% of dry, and rain within 2× of dry.